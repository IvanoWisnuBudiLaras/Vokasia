import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { decodeSessCookie, deleteSession, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { SESSION_COOKIE } from "@/lib/session";
import { getOidcClientSecret, getRuntimeUrl } from "@/lib/runtimeUrls";

const BFF_CLIENT_ID = "vokasia-bff";

/** VOK-H2-E3 handleLogout — hapus sesi Redis + revoke refresh di OpenIddict (FR-AUTH-04, instan) + clear cookie. */
export async function POST() {
  const store = await cookies();
  const sessionId = decodeSessCookie(store.get(SESS_COOKIE_NAME)?.value);

  if (sessionId) {
    const data = await deleteSession(sessionId);
    if (data?.refreshToken) {
      const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
      await fetch(new URL("/connect/revoke", apiBase), {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams({
          token: data.refreshToken,
          token_type_hint: "refresh_token",
          client_id: BFF_CLIENT_ID,
          client_secret: getOidcClientSecret(),
        }),
        cache: "no-store",
      }).catch(() => {
        // Logout FE tidak boleh gagal walau revoke API sempat error transient — sesi Redis
        // (sumber kebenaran proxyWithBearer) sudah dihapus di atas terlepas dari hasil ini.
      });
    }
  }

  const apiPublicBase = getRuntimeUrl("API_PUBLIC_URL", "http://localhost:5000");
  const res = NextResponse.redirect(new URL("/account/logout", apiPublicBase), { status: 303 });

  const clearOptions = {
    path: "/",
    httpOnly: true,
    secure: process.env.NODE_ENV === "production",
    sameSite: "lax" as const,
    maxAge: 0,
    expires: new Date(0),
  };

  res.cookies.set(SESS_COOKIE_NAME, "", clearOptions);
  res.cookies.set(SESSION_COOKIE, "", clearOptions);
  res.cookies.set("Cookies", "", clearOptions);
  res.cookies.set(".AspNetCore.Cookies", "", clearOptions);
  res.cookies.set(".AspNetCore.Identity.Application", "", clearOptions);
  res.cookies.set("vok_antiforgery", "", clearOptions);

  res.cookies.delete({ name: SESS_COOKIE_NAME, path: "/" });
  res.cookies.delete({ name: SESSION_COOKIE, path: "/" });
  res.cookies.delete({ name: ".AspNetCore.Cookies", path: "/" });
  res.cookies.delete({ name: ".AspNetCore.Identity.Application", path: "/" });

  return res;
}

export async function GET() {
  return POST();
}
