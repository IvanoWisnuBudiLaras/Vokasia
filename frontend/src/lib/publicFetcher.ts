/**
 * VOK-H6-E2 §2 — fetch server-side TANPA sesi, khusus 2 halaman publik (`/p/[slug]`, `/verify/
 * [code]`). BEDA dari `fetcher.ts` (WAJIB sesi cookie, dipakai Server Component role-protected) —
 * endpoint di sini (`GetPublicPortfolio`, `VerifyCertificate`) anonim by design di backend, tak
 * pernah butuh Authorization header sama sekali.
 *
 * [BUG NYATA ditemukan+ditambal sesi VOK-H6-E3, lihat DECISIONS.md D39]: versi H6-E2 SELALU
 * menambahkan prefix "/api" ke `path` di sini — TERNYATA backend PortfolioEndpoints.MapPortfolioEndpoints
 * mendaftarkan GetPublicPortfolio LANGSUNG di `app.MapGet("/p/{slug}", ...)` (TANPA "/api", beda dari
 * kelompok `/api/portfolio` yang StudentSelf), sementara VerifyCertificate justru terdaftar DI BAWAH
 * "/api/verify/{certCode}" (DENGAN "/api") — dua endpoint publik ini TIDAK KONSISTEN prefix-nya di
 * backend. Prefix otomatis di sini membuat halaman portofolio publik SELALU memanggil "/api/p/{slug}"
 * yang tidak pernah ada (404 permanen, walau slug valid+published) — lolos `bun run build` (cuma cek
 * tipe, bukan smoke test runtime nyata) dan baru ketahuan saat verifikasi H6-E3 menelusuri ulang rute
 * publik utk soal rate-limit. Diperbaiki dgn cara paling jujur: HAPUS asumsi prefix otomatis di sini,
 * caller (page.tsx masing2) WAJIB menulis path LENGKAP persis sesuai rute backend asli.
 *
 * Public page fetches are deliberately `no-store`: publication and hide/revoke state must become
 * authoritative immediately, including when an earlier route response was rendered. The backend
 * still advertises a controlled five-minute Cache-Control policy for direct public API consumers;
 * the Next BFF prioritizes credential/publication correctness over ISR staleness.
 */
export async function publicFetcher<T>(
  fullPath: string,
  revalidateSeconds = 300,
  cacheTags: string[] = []
): Promise<{ status: number; data: T | null }> {
  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const url = new URL(fullPath, apiBase);

  void revalidateSeconds;
  void cacheTags;
  const res = await fetch(url, { cache: "no-store" });

  if (res.status === 404) {
    return { status: 404, data: null };
  }

  if (!res.ok) {
    throw new Error(`publicFetcher ${fullPath} -> ${res.status}`);
  }

  return { status: res.status, data: (await res.json()) as T };
}
