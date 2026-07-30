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
  /** VOK-H6-E3 §2: nama SuperAdmin ASLI bila sesi ini SEDANG impersonasi (undefined = normal). Dibaca ImpersonationBanner (Server Component, root layout) — TANPA panggil Redis/DB, konsisten alasan cookie "lite" ini ada sejak awal (getSessionEdge, proxy.ts). */
  impersonatorName?: string;
}

/** "Lite" = cara baca (tanpa DB), bukan bentuk data — field sama persis dgn Session penuh. */
export type SessionLite = Session;

export const SESSION_COOKIE = "vok_session";

/** Inti murni encode/decode — dipakai getSession, getSessionEdge, dan test (mock cookie). */
function sessionSecret(): string {
  const secret = process.env.SESSION_SECRET;
  if (secret && secret.length >= 16) return secret;
  if (process.env.NODE_ENV === "production") {
    throw new Error("SESSION_SECRET wajib diset (minimal 16 karakter) di production.");
  }
  return "development-only-session-secret";
}

function base64UrlEncode(value: Uint8Array): string {
  let binary = "";
  for (const byte of value) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, "");
}

function base64UrlDecode(value: string): Uint8Array {
  const padded = value.replace(/-/g, "+").replace(/_/g, "/") + "=".repeat((4 - (value.length % 4)) % 4);
  const binary = atob(padded);
  return Uint8Array.from(binary, (character) => character.charCodeAt(0));
}

async function sign(payload: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(sessionSecret()),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign", "verify"],
  );
  return base64UrlEncode(new Uint8Array(await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(payload))));
}

async function verify(payload: string, signature: string): Promise<boolean> {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(sessionSecret()),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["verify"],
  );
  return crypto.subtle.verify(
    "HMAC",
    key,
    base64UrlDecode(signature) as BufferSource,
    new TextEncoder().encode(payload),
  );
}

export async function decodeSessionCookie(raw: string | undefined | null): Promise<Session | null> {
  if (!raw) return null;

  try {
    const separator = raw.lastIndexOf(".");
    if (separator <= 0) return null;
    const payload = raw.slice(0, separator);
    const expected = raw.slice(separator + 1);
    if (!await verify(payload, expected)) return null;

    const parsed = JSON.parse(new TextDecoder().decode(base64UrlDecode(payload))) as Partial<Session>;
    if (!parsed.id || !parsed.role) return null;

    return {
      id: parsed.id,
      name: parsed.name ?? "",
      role: parsed.role,
      tenantId: parsed.tenantId ?? null,
      impersonatorName: parsed.impersonatorName,
    };
  } catch {
    return null;
  }
}

/** Kebalikan decodeSessionCookie — dipakai H2-E3 (set cookie saat login sukses) & test (mock cookie). */
export async function encodeSessionCookie(session: Session): Promise<string> {
  const payload = base64UrlEncode(new TextEncoder().encode(JSON.stringify(session)));
  return `${payload}.${await sign(payload)}`;
}

interface EdgeCookieReader {
  cookies: { get(name: string): { value: string } | undefined };
}

/**
 * Middleware/proxy.ts — TANPA panggil DB (persyaratan ticket). Menerima apa pun berbentuk
 * `.cookies.get(name)` — NextRequest asli ATAU objek tiruan di unit test (mock session cookie).
 */
export async function getSessionEdge(req: EdgeCookieReader): Promise<SessionLite | null> {
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
