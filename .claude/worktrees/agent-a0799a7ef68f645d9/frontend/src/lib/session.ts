/**
 * VOK-H2-E2 §lib/session.ts — satu-satunya tempat FE membaca "siapa yang login".
 *
 * Tidak pernah menyentuh token asli (AGENTS.md #3: token hanya di BFF/Redis; browser cuma
 * httpOnly Secure SameSite=Lax cookie; dilarang localStorage/sessionStorage). Fungsi di file ini
 * hanya membaca sebuah cookie httpOnly kecil berisi klaim non-rahasia (id/name/role/tenantId) —
 * SAMA PERSIS dengan klaim yang sudah dipasang VokasiaClaimsFactory (H1-E3) ke access token
 * (sub/tenant_id/role/name) — bukan token itu sendiri, jadi tidak menambah permukaan rahasia baru.
 *
 * [ASSUMPTION] H2-E3 (BFF, belum diimplementasi penuh — lihat DECISIONS.md D15/D16) diasumsikan
 * menulis cookie `vok_session` ini (httpOnly, Secure, SameSite=Lax) saat `POST /api/auth/login`
 * sukses, berisi base64url(JSON {id,name,role,tenantId}), TERPISAH dari cookie sesi opaque
 * Redis-nya sendiri. Alasan dipisah: proxy.ts (VOK-H2-E2) wajib baca role TANPA panggil DB/Redis
 * (lihat getSessionEdge di bawah) — kalau kontrak asli H2-E3 berbeda, cukup ubah SESSION_COOKIE +
 * decodeSessionCookie di SINI, satu titik, tidak menyebar ke proxy.ts/dashboard manapun.
 *
 * Catatan keamanan: cookie ini BUKAN batas keamanan (AGENTS.md #2 — RBAC ditegakkan di endpoint
 * API, bukan di UI). Isinya bukan rahasia (persis klaim JWT yang toh bisa didekode siapa saja),
 * jadi tanpa tanda tangan/HMAC cukup untuk MVP; endpoint API tetap memvalidasi access token asli
 * secara independen. Rekomendasi utk H2-E3: tanda-tangani cookie ini (mis. HMAC) sbg pertahanan
 * berlapis — dicatat di sini sbg saran, bukan blocker ticket ini.
 */

export type Role =
  | "SuperAdmin"
  | "TenantAdmin"
  | "DeptHead"
  | "Teacher"
  | "IndustryMentor"
  | "Student"
  | "ParentViewer";

export interface Session {
  id: string;
  name: string;
  role: Role;
  tenantId: string | null;
}

/** "Lite" = cara baca (tanpa DB), bukan bentuk data — field sama persis dgn Session penuh. */
export type SessionLite = Session;

export const SESSION_COOKIE = "vok_session";

/** Inti murni encode/decode — dipakai getSession, getSessionEdge, dan test (mock cookie). */
export function decodeSessionCookie(raw: string | undefined | null): Session | null {
  if (!raw) return null;

  try {
    const json = Buffer.from(raw, "base64url").toString("utf-8");
    const parsed = JSON.parse(json) as Partial<Session>;
    if (!parsed.id || !parsed.role) return null;

    return {
      id: parsed.id,
      name: parsed.name ?? "",
      role: parsed.role,
      tenantId: parsed.tenantId ?? null,
    };
  } catch {
    return null; // cookie rusak/dipalsu asal-asalan -> perlakukan sbg belum login, jangan crash.
  }
}

/** Kebalikan decodeSessionCookie — dipakai H2-E3 (set cookie saat login sukses) & test (mock cookie). */
export function encodeSessionCookie(session: Session): string {
  return Buffer.from(JSON.stringify(session), "utf-8").toString("base64url");
}

interface EdgeCookieReader {
  cookies: { get(name: string): { value: string } | undefined };
}

/**
 * Middleware/proxy.ts — TANPA panggil DB (persyaratan ticket). Menerima apa pun berbentuk
 * `.cookies.get(name)` — NextRequest asli ATAU objek tiruan di unit test (mock session cookie).
 */
export function getSessionEdge(req: EdgeCookieReader): SessionLite | null {
  return decodeSessionCookie(req.cookies.get(SESSION_COOKIE)?.value);
}

/**
 * Server Components / Route Handlers / Server Actions — via next/headers (async, App Router).
 * JANGAN panggil di proxy.ts: next/headers.cookies() butuh request-scope render Next.js yang
 * tidak tersedia saat eksekusi Proxy (proxy "invoked separately of render code" — dok Next 16).
 * Untuk proxy.ts pakai getSessionEdge(req) di atas.
 */
export async function getSession(): Promise<Session | null> {
  const { cookies } = await import("next/headers");
  const store = await cookies();
  return decodeSessionCookie(store.get(SESSION_COOKIE)?.value);
}
