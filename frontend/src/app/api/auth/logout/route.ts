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

  const appUrl = getRuntimeUrl("NEXT_PUBLIC_APP_URL", "http://localhost:3000");
  const res = NextResponse.redirect(new URL("/login", appUrl), { status: 303 });
  res.cookies.delete(SESS_COOKIE_NAME);
  res.cookies.delete(SESSION_COOKIE);
  return res;
}
