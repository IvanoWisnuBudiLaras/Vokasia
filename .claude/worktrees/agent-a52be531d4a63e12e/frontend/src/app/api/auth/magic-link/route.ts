import { NextResponse } from "next/server";
import { createSession, encodeSessCookie, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { roleHome } from "@/lib/roleHome";
import { encodeSessionCookie, SESSION_COOKIE, type Role } from "@/lib/session";

const BFF_CLIENT_ID = "vokasia-bff";
const MAGIC_LINK_GRANT_TYPE = "urn:vokasia:params:oauth:grant-type:magic-link";

interface TokenResponse {
  access_token: string;
  refresh_token?: string;
  expires_in: number;
}

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
 * VOK-H2-E3 §3 ExchangeMagicToken (sisi BFF) — cermin persis handleCallback
 * (api/auth/callback/route.ts): tukar sesuatu ke /connect/token, simpan {accessToken,
 * refreshToken,exp,user} di Redis, set cookie vok_sess (httpOnly, opaque) + vok_session (lite,
 * dibaca proxy.ts), redirect roleHome. BEDA dari callback: grant_type magic-link (bukan
 * authorization_code) dan TANPA state/PKCE — mentor klik SATU link dari halaman konfirmasi
 * /mentor-invite (yang sudah panggil ValidateMagicToken TANPA konsumsi), tidak ada dialog
 * browser bolak-balik seperti login form OAuth interaktif.
 *
 * Endpoint ini SENDIRI yang mengkonsumsi token (backend menandai UsedAt) — karena itu route ini
 * TIDAK dipanggil langsung dari email/link mentah; halaman /mentor-invite yang jadi perantara
 * (page.tsx di sana memanggil /api/mentor-invites/validate dulu, baru render tombol ke sini).
 */
export async function GET(req: Request) {
  const url = new URL(req.url);
  const token = url.searchParams.get("token");

  const appUrl = process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000";
  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";

  if (!token) {
    return NextResponse.redirect(new URL("/login?error=access_denied", appUrl));
  }

  const tokenRes = await fetch(new URL("/connect/token", apiBase), {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: MAGIC_LINK_GRANT_TYPE,
      token,
      client_id: BFF_CLIENT_ID,
      client_secret: process.env.OIDC_BFF_CLIENT_SECRET ?? "dev-only-secret-change-me",
    }),
    cache: "no-store",
  });

  if (!tokenRes.ok) {
    // Pesan generik (disiplin sama dgn MagicLinkService backend) — jangan bocorkan alasan spesifik.
    return NextResponse.redirect(new URL("/login?error=magic_link_invalid", appUrl));
  }

  const tokens = (await tokenRes.json()) as TokenResponse;
  const claims = decodeJwtPayload(tokens.access_token);
  const user = {
    id: String(claims.sub ?? ""),
    name: String(claims.name ?? ""),
    role: String(claims.role ?? "") as Role,
    tenantId: claims.tenant_id ? String(claims.tenant_id) : null,
  };

  const sessionId = await createSession({
    accessToken: tokens.access_token,
    accessExp: Date.now() + tokens.expires_in * 1000,
    refreshToken: tokens.refresh_token ?? "",
    user,
  });

  const dest = roleHome(user.role);
  const res = NextResponse.redirect(new URL(dest === "/login" ? "/" : dest, appUrl));

  const maxAge = 60 * 60 * 24 * 14; // 14 hari, cermin refresh token lifetime — sama dgn callback/route.ts.
  res.cookies.set(SESS_COOKIE_NAME, encodeSessCookie(sessionId), cookieOpts(maxAge));
  res.cookies.set(SESSION_COOKIE, encodeSessionCookie(user), cookieOpts(maxAge));

  return res;
}
