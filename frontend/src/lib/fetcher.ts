/**
 * Wrapper fetch tunggal ke BFF proxy (/api/proxy/*). Kerangka H1 — auth (Bearer, refresh)
 * dipasang H2-E3 (proxyWithBearer). Semua panggilan API dari FE WAJIB lewat sini, bukan fetch
 * langsung, agar token tidak pernah terekspos ke kode client.
 */
export async function fetcher<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api/proxy${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  if (!res.ok) {
    const body = await res.text().catch(() => "");
    throw new Error(`fetcher ${path} -> ${res.status}: ${body}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}
