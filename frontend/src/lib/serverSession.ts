import { cookies } from "next/headers";
import { decodeSessCookie, getSessionData, SESS_COOKIE_NAME } from "./bffSession";
import { getSession as getLiteSession, SESSION_COOKIE, type Session } from "./session";

/**
 * Server-side session verification.
 * Mengecek keabsahan cookie lite DAN mencocokkannya dengan session ID di Redis.
 * Jika Redis restart/mati, fungsi ini langsung menghapus cookie sesi di browser.
 */
export async function getVerifiedSession(): Promise<Session | null> {
  const lite = await getLiteSession();
  if (!lite) return null;

  try {
    const store = await cookies();
    const sessionIdCookie = store.get(SESS_COOKIE_NAME)?.value;
    const sessionId = decodeSessCookie(sessionIdCookie);

    if (!sessionId) {
      await clearSessionCookies();
      return null;
    }

    const bffSession = await getSessionData(sessionId);
    if (!bffSession) {
      await clearSessionCookies();
      return null;
    }

    return lite;
  } catch {
    return null;
  }
}

async function clearSessionCookies() {
  try {
    const store = await cookies();
    store.delete(SESS_COOKIE_NAME);
    store.delete(SESSION_COOKIE);
  } catch {
    // Abaikan error di environment non-mutasi
  }
}
