/**
 * VOK-H6-E2 §2 — fetch server-side TANPA sesi, khusus 2 halaman publik (`/p/[slug]`, `/verify/
 * [code]`). BEDA dari `fetcher.ts` (WAJIB sesi cookie, dipakai Server Component role-protected) —
 * endpoint di sini (`GetPublicPortfolio`, `VerifyCertificate`) anonim by design di backend, tak
 * pernah butuh Authorization header sama sekali.
 *
 * `next: { revalidate }` dipasang supaya Next.js App Router men-cache respons di edge/server (ISR
 * fetch cache) SELARAS dgn `Cache-Control: public, max-age=300` yang backend sendiri kirim utk
 * GetPublicPortfolio (AC ticket literal: "cacheable Cache-Control 5 mnt") — 404 (`notFound()`
 * dipanggil caller) TIDAK ikut di-cache (Next.js tidak cache response yang dilempar sbg exception).
 */
export async function publicFetcher<T>(path: string, revalidateSeconds = 300): Promise<{ status: number; data: T | null }> {
  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  const url = new URL(`/api${path}`, apiBase);

  const res = await fetch(url, { next: { revalidate: revalidateSeconds } });

  if (res.status === 404) {
    return { status: 404, data: null };
  }

  if (!res.ok) {
    throw new Error(`publicFetcher ${path} -> ${res.status}`);
  }

  return { status: res.status, data: (await res.json()) as T };
}
