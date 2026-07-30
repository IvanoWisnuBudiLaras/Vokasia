import { cookies } from "next/headers";
import { decodeSessCookie, getSessionData, SESS_COOKIE_NAME } from "@/lib/bffSession";
import { refreshOnExpiry } from "@/lib/refresh";

/**
 * Wrapper fetch tunggal ke API — dipanggil dari Server Component (satu-satunya pemanggil saat
 * ini: app/(school)/app/page.tsx, dan sejenisnya utk role lain nanti). Kerangka H1 sengaja
 * menembak "/api/proxy${path}" (URL relatif); itu BENAR utk fetch dari BROWSER (client component)
 * tapi SALAH di sini.
 *
 * GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): fetch() sisi server Next.js (dipanggil
 * selama SSR di proses Node, bukan dari browser) (1) tidak punya origin implisit utk resolve URL
 * relatif — beda dgn fetch browser yg resolve relatif ke window.location — jadi "/api/proxy/..."
 * gagal di-parse sama sekali (exception, tertangkap try/catch page.tsx -> selalu jatuh ke
 * ErrorState "proxy BFF belum tersedia", TERLEPAS dari proxy itu sendiri sudah ada/tidak sejak
 * ticket ini); (2) TIDAK otomatis membawa cookie request masuk (tiap fetch() sisi server adalah
 * request KELUAR baru & terpisah) — jadi meski URL diperbaiki jadi absolut, muter balik ke
 * /api/proxy sendiri tetap butuh cookie diteruskan manual. Solusi: baca sesi LANGSUNG di sini
 * (next/headers cookies() — valid krn fetcher HANYA dipanggil dari Server Component, tidak
 * pernah dari "use client") dan panggil backend LANGSUNG (skip hop /api/proxy sendiri) — aman
 * krn Server Component TIDAK PERNAH mengirim variabel internal (termasuk access token yg
 * sempat dipegang di memori selama SSR) ke browser, hanya HTML/RSC payload hasil render; properti
 * keamanan "token tak pernah ke JS client" (alasan /api/proxy ada) tetap utuh krn itu soal APA
 * yang dikirim ke browser, bukan apa yang boleh disentuh di server. /api/proxy/* TETAP satu-
 * satunya jalur yang benar bila kelak ada CLIENT COMPONENT yang fetch (mis. form interaktif H3+)
 * — fetcher() ini TIDAK dipakai dari sana, panggil /api/proxy langsung via fetch browser biasa.
 *
 * Retry-once-after-refresh di sini SENGAJA menduplikasi logic proxyWithBearer
 * (app/api/proxy/[...path]/route.ts) alih-alih diekstrak jadi 1 helper bersama — route proxy itu
 * sudah diverifikasi jalan lewat smoke test HTTP nyata; menghindari menyentuhnya lagi di sesi yang
 * sama demi alasan DRY murni. Kandidat refactor sesi berikutnya kalau ada pemanggil ke-3.
 */
export async function fetcher<T>(path: string, init?: RequestInit): Promise<T> {
  const store = await cookies();
  const sessionId = decodeSessCookie(store.get(SESS_COOKIE_NAME)?.value);
  if (!sessionId) {
    throw new Error(`fetcher ${path} -> belum login (tanpa sesi sisi server).`);
  }

  const session = await getSessionData(sessionId);
  if (!session) {
    throw new Error(`fetcher ${path} -> sesi tidak ditemukan atau kedaluwarsa.`);
  }

  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const url = new URL(`/api${path}`, apiBase);

  const doFetch = (accessToken: string) =>
    fetch(url, {
      ...init,
      headers: { "Content-Type": "application/json", ...init?.headers, Authorization: `Bearer ${accessToken}` },
      cache: "no-store",
    });

  let res = await doFetch(session.accessToken);
  if (res.status === 401) {
    const refreshed = await refreshOnExpiry(sessionId);
    if (refreshed.ok) {
      res = await doFetch(refreshed.accessToken);
    }
  }

  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`fetcher ${path} -> ${res.status}: ${body}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}
