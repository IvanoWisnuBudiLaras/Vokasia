import { getSessionData, revokeAllSessionsForUser, updateSessionTokens } from "./bffSession";

const BFF_CLIENT_ID = "vokasia-bff";

export type RefreshResult =
  | { ok: true; accessToken: string }
  | { ok: false; reason: "no-session" | "no-refresh-token" | "reuse-detected" };

async function writeAuditLog(accessToken: string, action: string, entity: string, entityId: string, meta: unknown) {
  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  try {
    await fetch(new URL("/api/audit", apiBase), {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: `Bearer ${accessToken}` },
      body: JSON.stringify({ action, entity, entityId, metaJson: JSON.stringify(meta) }),
      cache: "no-store",
    });
  } catch {
    // Audit adalah best-effort dari sisi BFF (bukan satu-satunya bukti) — kegagalan tulis audit
    // TIDAK BOLEH membatalkan revocation sesi yang sudah terjadi (fail-safe, bukan fail-open).
  }
}

/**
 * VOK-H2-E3 refreshOnExpiry — rotation: OpenIddict (`UseReferenceRefreshTokens`) menolak refresh
 * token yang sudah dipakai/di-rotasi dengan `invalid_grant`. [ASSUMPTION]: OpenIddict tidak
 * membedakan lewat body error apakah ini "reuse" (refresh lama dipakai lagi, indikasi token
 * dicuri) vs "memang sudah kedaluwarsa/dicabut" — dari luar keduanya sama-sama invalid_grant.
 * Tanpa melacak hash refresh terakhir sendiri di lapisan BFF (kompleksitas tambahan di luar
 * bujet ticket ini), pilihan aman: PERLAKUKAN SAMA sbg sinyal cabut (fail-closed), sesuai AC
 * "reuse refresh lama -> seluruh keluarga sesi tercabut" — locking-out sesi yang memang sudah
 * kedaluwarsa bukan kerugian (harusnya re-login lagi toh).
 */
export async function refreshOnExpiry(sessionId: string): Promise<RefreshResult> {
  const session = await getSessionData(sessionId);
  if (!session) return { ok: false, reason: "no-session" };
  if (!session.refreshToken) return { ok: false, reason: "no-refresh-token" };

  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const res = await fetch(new URL("/connect/token", apiBase), {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "refresh_token",
      refresh_token: session.refreshToken,
      client_id: BFF_CLIENT_ID,
      client_secret: process.env.OIDC_BFF_CLIENT_SECRET ?? "dev-only-secret-change-me",
    }),
    cache: "no-store",
  });

  if (!res.ok) {
    await revokeAllSessionsForUser(session.user.id);
    await writeAuditLog(session.accessToken, "TokenReuseDetected", "AppUser", session.user.id, {
      sessionId,
      detectedAt: new Date().toISOString(),
    });
    return { ok: false, reason: "reuse-detected" };
  }

  const tokens = (await res.json()) as { access_token: string; refresh_token?: string; expires_in: number };
  await updateSessionTokens(
    sessionId,
    tokens.access_token,
    Date.now() + tokens.expires_in * 1000,
    tokens.refresh_token ?? session.refreshToken
  );
  return { ok: true, accessToken: tokens.access_token };
}
