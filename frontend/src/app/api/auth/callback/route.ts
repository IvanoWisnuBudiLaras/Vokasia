import { NextResponse } from "next/server";
import { consumePkce, createSession, encodeSessCookie, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { getSafeLocalReturnUrl } from "@/lib/localReturnUrl";
import { roleHome } from "@/lib/roleHome";
import { encodeSessionCookie, SESSION_COOKIE, type Role } from "@/lib/session";
import { getOidcClientSecret, getRuntimeUrl } from "@/lib/runtimeUrls";

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

function cookieOpts(maxAgeSeconds: number, appUrl: string) {
  const isSecure = process.env.NODE_ENV === "production" && appUrl.startsWith("https://");
  return {
    httpOnly: true,
    secure: isSecure,
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

  const appUrl = getRuntimeUrl("NEXT_PUBLIC_APP_URL", "http://localhost:3000");
  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";

  if (oauthError || !code || !state) {
    console.warn(`[BFF Callback] OAuth error or missing params: error=${oauthError}, code=${!!code}, state=${!!state}`);
    return NextResponse.redirect(new URL("/login?error=access_denied", appUrl));
  }

  const pkceRaw = await consumePkce(state);
  if (!pkceRaw) {
    console.warn(`[BFF Callback] PKCE state consume failed or expired for state=${state}`);
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
      client_secret: getOidcClientSecret(),
      code_verifier: verifier,
    }),
    cache: "no-store",
  });

  if (!tokenRes.ok) {
    const errorText = await tokenRes.text().catch(() => "");
    console.warn(`[BFF Callback] Token exchange failed with status ${tokenRes.status}: ${errorText}`);
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

  const dest = getSafeLocalReturnUrl(next) ?? roleHome(user.role);
  const res = NextResponse.redirect(new URL(dest === "/login" ? "/" : dest, appUrl));

  const maxAge = 60 * 60 * 24 * 14; // 14 hari, cermin refresh token lifetime.
  res.cookies.set(SESS_COOKIE_NAME, encodeSessCookie(sessionId), cookieOpts(maxAge, appUrl));
  // Cookie "lite" (VOK-H2-E2 lib/session.ts) — role/tenantId dibaca proxy.ts, TANPA token.
  res.cookies.set(SESSION_COOKIE, await encodeSessionCookie(user), cookieOpts(maxAge, appUrl));

  return res;
}
