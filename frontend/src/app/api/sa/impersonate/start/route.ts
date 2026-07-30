import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { decodeSessCookie, getSessionData, startImpersonation, SESS_COOKIE_NAME, type BffSessionUser } from "@/lib/bffSession";
import { roleHome } from "@/lib/roleHome";
import { encodeSessionCookie, SESSION_COOKIE, type Role } from "@/lib/session";

const BFF_CLIENT_ID = "vokasia-bff";
const IMPERSONATION_GRANT_TYPE = "urn:vokasia:params:oauth:grant-type:impersonation";

function decodeJwtPayload(jwt: string): Record<string, unknown> {
  const parts = jwt.split(".");
  return JSON.parse(Buffer.from(parts[1], "base64url").toString("utf-8"));
}

function cookieOpts(maxAgeSeconds: number) {
  return {
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    path: "/",
    maxAge: maxAgeSeconds,
  };
}

/**
 * VOK-H6-E3 §2 StartImpersonation (BFF) — dipanggil client component tombol "Login sebagai" di
 * sa/tenants (SA memilih staf tenant lewat GET /sa/tenants/{id}/staff, backend H6-E3). Menukar
 * access token OpenIddict grant kustom `impersonation` (Authorization: Bearer = access token SA
 * SENDIRI dari sesi Redis saat ini — backend AuthorizationController.Exchange() membaca
 * HttpContext.User dari header ini utk memverifikasi pemanggil benar SuperAdmin, BUKAN dari body),
 * lalu men-stash identitas SA & menimpa sesi Redis (sessionId/cookie vok_sess TETAP) dgn identitas
 * target — lihat startImpersonation() di bffSession.ts.
 */
export async function POST(req: Request) {
  const store = await cookies();
  const sessionId = decodeSessCookie(store.get(SESS_COOKIE_NAME)?.value);
  if (!sessionId) {
    return NextResponse.json({ message: "Belum login." }, { status: 401 });
  }

  const session = await getSessionData(sessionId);
  if (!session || session.user.role !== "SuperAdmin") {
    return NextResponse.json({ message: "Hanya SuperAdmin yang boleh memulai impersonasi." }, { status: 403 });
  }

  const body = (await req.json().catch(() => null)) as { targetUserId?: string } | null;
  if (!body?.targetUserId) {
    return NextResponse.json({ message: "targetUserId wajib diisi." }, { status: 400 });
  }

  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const tokenRes = await fetch(new URL("/connect/token", apiBase), {
    method: "POST",
    headers: {
      "Content-Type": "application/x-www-form-urlencoded",
      Authorization: `Bearer ${session.accessToken}`,
    },
    body: new URLSearchParams({
      grant_type: IMPERSONATION_GRANT_TYPE,
      target_user_id: body.targetUserId,
      client_id: BFF_CLIENT_ID,
      client_secret: process.env.OIDC_BFF_CLIENT_SECRET ?? "dev-only-secret-change-me",
    }),
    cache: "no-store",
  });

  if (!tokenRes.ok) {
    const errBody = await tokenRes.json().catch(() => ({}));
    return NextResponse.json(
      { message: errBody.error_description ?? "Gagal memulai impersonasi." },
      { status: 400 }
    );
  }

  const tokens = (await tokenRes.json()) as { access_token: string; expires_in: number };
  const claims = decodeJwtPayload(tokens.access_token);
  const targetUser: BffSessionUser = {
    id: String(claims.sub ?? ""),
    name: String(claims.name ?? ""),
    role: String(claims.role ?? "") as Role,
    tenantId: claims.tenant_id ? String(claims.tenant_id) : null,
  };

  await startImpersonation(sessionId, {
    accessToken: tokens.access_token,
    accessExp: Date.now() + tokens.expires_in * 1000,
    user: targetUser,
  });

  const dest = roleHome(targetUser.role);
  const res = NextResponse.json({ redirectTo: dest });

  const maxAge = 60 * 60 * 24 * 14;
  // Cookie "lite" ditimpa jadi identitas TARGET (proxy.ts pakai ini utk guard matrix) + impersonatorName
  // (SA asli) supaya ImpersonationBanner bisa tampil TANPA baca Redis.
  res.cookies.set(
    SESSION_COOKIE,
    await encodeSessionCookie({ id: targetUser.id, name: targetUser.name, role: targetUser.role, tenantId: targetUser.tenantId, impersonatorName: session.user.name }),
    cookieOpts(maxAge)
  );

  return res;
}
