import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { decodeSessCookie, getSessionData, SESS_COOKIE_NAME } from "@/lib/bffSession";

/** VOK-H2-E3 handleSession — baca Redis via cookie vok_sess -> {user:{...}}. Tanpa token di response (AC). */
export async function GET() {
  const store = await cookies();
  const sessionId = decodeSessCookie(store.get(SESS_COOKIE_NAME)?.value);
  if (!sessionId) {
    return NextResponse.json({ user: null }, { status: 401 });
  }

  const data = await getSessionData(sessionId);
  if (!data) {
    return NextResponse.json({ user: null }, { status: 401 });
  }

  return NextResponse.json({ user: data.user });
}
