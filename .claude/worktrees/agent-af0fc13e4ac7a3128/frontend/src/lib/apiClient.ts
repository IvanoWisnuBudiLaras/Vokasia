/**
 * VOK-H3-E2 — wrapper fetch utk CLIENT COMPONENT (JournalForm, PhotoUploader, ApprovalList, dst).
 * Selalu lewat "/api/proxy/*" (URL relatif, resolve ke window.location di browser) — TIDAK PERNAH
 * menyentuh token (proxyWithBearer, VOK-H2-E3, menempelkan Authorization di sisi server). Ini
 * SENGAJA berbeda dari lib/fetcher.ts (Server Component only, baca cookies() next/headers
 * langsung) — lihat doc-comment fetcher.ts: "kalau kelak ada CLIENT COMPONENT yang fetch, panggil
 * /api/proxy langsung via fetch browser biasa", persis yang dilakukan file ini.
 *
 * Bentuk body error backend TIDAK SERAGAM antar jalur (ditemukan sesi H3-E3): validasi
 * FluentValidation -> Results.ValidationProblem (ProblemDetails: {title, errors:{Field:[msg]}});
 * DomainImmutableException/rate-limit -> {code, message}; Conflict/Forbid manual -> {message}
 * kadang tanpa body sama sekali (Forbid()). extractMessage() coba semua bentuk, fallback generik.
 */

export class ApiError extends Error {
  status: number;
  body: unknown;

  constructor(status: number, body: unknown, message: string) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

function extractMessage(status: number, data: unknown): string {
  if (data && typeof data === "object") {
    const obj = data as Record<string, unknown>;

    if (typeof obj.message === "string" && obj.message.length > 0) {
      return obj.message;
    }

    if (obj.errors && typeof obj.errors === "object") {
      const errors = obj.errors as Record<string, unknown>;
      const firstKey = Object.keys(errors)[0];
      const firstVal = firstKey ? errors[firstKey] : undefined;
      if (Array.isArray(firstVal) && typeof firstVal[0] === "string") {
        return firstVal[0];
      }
    }

    if (typeof obj.title === "string" && obj.title.length > 0) {
      return obj.title;
    }
  }

  if (status === 429) return "Terlalu banyak percobaan. Coba lagi sebentar lagi.";
  if (status === 409) return "Data ini sudah berubah — muat ulang halaman.";
  if (status === 403) return "Kamu tidak punya akses untuk aksi ini.";
  return `Permintaan gagal (${status}).`;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`/api/proxy${path}`, {
    ...init,
    headers: { "Content-Type": "application/json", ...init?.headers },
  });

  if (res.status === 204) {
    return undefined as T;
  }

  const text = await res.text();
  const data = text.length > 0 ? JSON.parse(text) : undefined;

  if (!res.ok) {
    throw new ApiError(res.status, data, extractMessage(res.status, data));
  }

  return data as T;
}

export const apiClient = {
  get: <T>(path: string): Promise<T> => request<T>(path, { method: "GET" }),
  post: <T>(path: string, body?: unknown): Promise<T> =>
    request<T>(path, { method: "POST", body: body !== undefined ? JSON.stringify(body) : undefined }),
};
