import { NextResponse } from "next/server";
import { consumePkce, createSession, encodeSessCookie, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { roleHome } from "@/lib/roleHome";
import { encodeSessionCookie, SESSION_COOKIE, type Role } from "@/lib/session";

const BFF_CLIENT_ID = "vokasia-bff";

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

/** VOK-H2-E3 handleCallback — validasi state, tukar code (PKCE), simpan sesi di Redis, set cookie, redirect roleHome. */
export async function GET(req: Request) {
  const url = new URL(req.url);
  const code = url.searchParams.get("code");
  const state = url.searchParams.get("state");
  const oauthError = url.searchParams.get("error");

  const appUrl = process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000";
  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";

  if (oauthError || !code || !state) {
    return NextResponse.redirect(new URL("/login?error=access_denied", appUrl));
  }

  const pkceRaw = await consumePkce(state);
  if (!pkceRaw) {
    // state tak dikenal/kedaluwarsa/sudah dipakai -> tolak (anti CSRF & anti replay).
    return NextResponse.redirect(new URL("/login?error=unauthenticated", appUrl));
  }
  const { verifier, next } = JSON.parse(pkceRaw) as { verifier: string; next: string };

  const tokenRes = await fetch(new URL("/connect/token", apiBase), {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: new URLSearchParams({
      grant_type: "authorization_code",
      code,
      redirect_uri: `${appUrl}/api/auth/callback`,
      client_id: BFF_CLIENT_ID,
      client_secret: process.env.OIDC_BFF_CLIENT_SECRET ?? "dev-only-secret-change-me",
      code_verifier: verifier,
    }),
    cache: "no-store",
  });

  if (!tokenRes.ok) {
    return NextResponse.redirect(new URL("/login?error=access_denied", appUrl));
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

  const dest = next && next !== "" ? next : roleHome(user.role);
  const res = NextResponse.redirect(new URL(dest === "/login" ? "/" : dest, appUrl));

  const maxAge = 60 * 60 * 24 * 14; // 14 hari, cermin refresh token lifetime.
  res.cookies.set(SESS_COOKIE_NAME, encodeSessCookie(sessionId), cookieOpts(maxAge));
  // Cookie "lite" (VOK-H2-E2 lib/session.ts) — role/tenantId dibaca proxy.ts, TANPA token.
  res.cookies.set(SESSION_COOKIE, encodeSessionCookie(user), cookieOpts(maxAge));

  return res;
}
