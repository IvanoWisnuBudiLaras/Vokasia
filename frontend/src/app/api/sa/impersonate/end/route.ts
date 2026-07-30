import { cookies } from "next/headers";
import { NextResponse } from "next/server";
import { decodeSessCookie, endImpersonation, getSessionData, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { roleHome } from "@/lib/roleHome";
import { encodeSessionCookie, SESSION_COOKIE } from "@/lib/session";

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
 * VOK-H6-E3 §2 EndImpersonation (BFF) — kembalikan sesi Redis ke identitas SuperAdmin ASLI yang
 * di-stash StartImpersonation. Panggil backend /api/impersonation/end LEBIH DULU (selagi
 * accessToken TARGET+klaim impersonator_id masih dipakai di header Authorization — begitu
 * endImpersonation() BFF jalan, tak ada lagi cara backend tahu siapa yang barusan mengakhiri)
 * utk menulis AuditLog "ImpersonationEnded"; kegagalan panggilan itu TIDAK memblokir pemulihan
 * sesi (best-effort, di-log bukan dilempar — SA tidak boleh "terjebak" jadi target selamanya
 * hanya krn 1 panggilan audit gagal transient).
 */
export async function POST() {
  const store = await cookies();
  const sessionId = decodeSessCookie(store.get(SESS_COOKIE_NAME)?.value);
  if (!sessionId) {
    return NextResponse.json({ message: "Belum login." }, { status: 401 });
  }

  const session = await getSessionData(sessionId);
  if (!session?.impersonation) {
    return NextResponse.json({ message: "Tidak sedang dalam sesi impersonasi." }, { status: 400 });
  }

  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  await fetch(new URL("/api/impersonation/end", apiBase), {
    method: "POST",
    headers: { Authorization: `Bearer ${session.accessToken}` },
    cache: "no-store",
  }).catch((err) => {
    console.error("[api/sa/impersonate/end] gagal mencatat audit ImpersonationEnded:", err);
  });

  const restored = await endImpersonation(sessionId);
  if (!restored) {
    return NextResponse.json({ message: "Sesi impersonasi tidak ditemukan." }, { status: 400 });
  }

  const dest = roleHome(restored.user.role);
  const res = NextResponse.json({ redirectTo: dest });

  const maxAge = 60 * 60 * 24 * 14;
  res.cookies.set(
    SESSION_COOKIE,
    await encodeSessionCookie({ id: restored.user.id, name: restored.user.name, role: restored.user.role, tenantId: restored.user.tenantId }),
    cookieOpts(maxAge)
  );

  return res;
}
