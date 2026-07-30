import { roleHome } from "./roleHome";
import type { Role, SessionLite } from "./session";

export type GuardDecision = { type: "allow" } | { type: "redirect"; to: string };

/** Segment terproteksi × role yang boleh masuk (VOK-H2-E2 guard matrix, PRD 2.3). */
export const SEGMENT_ROLES: Record<string, Role[]> = {
  "/sa": ["SuperAdmin"],
  "/app": ["TenantAdmin", "DeptHead", "Teacher"],
  "/mentor": ["IndustryMentor"],
  "/student": ["Student"],
};

const PUBLIC_EXACT = new Set(["/", "/login"]);
const PUBLIC_PREFIXES = ["/p/", "/verify/"];

function isPublic(pathname: string): boolean {
  if (PUBLIC_EXACT.has(pathname)) return true;
  return PUBLIC_PREFIXES.some((prefix) => pathname.startsWith(prefix));
}

function matchSegment(pathname: string): string | null {
  for (const segment of Object.keys(SEGMENT_ROLES)) {
    if (pathname === segment || pathname.startsWith(`${segment}/`)) {
      return segment;
    }
  }
  return null;
}

/**
 * Fungsi murni — inti guard matrix, dites langsung tanpa Next.js runtime (AC: "boleh test
 * middleware unit"). proxy.ts (wrapper tipis Next.js) memanggil ini lalu menerjemahkan
 * GuardDecision -> NextResponse.redirect/next. Tidak pernah melempar exception.
 */
export function resolveGuardDecision(pathname: string, session: SessionLite | null): GuardDecision {
  if (isPublic(pathname)) {
    return { type: "allow" };
  }

  const segment = matchSegment(pathname);
  if (segment === null) {
    return { type: "allow" }; // di luar 4 segment terproteksi (mis. /api/*) — bukan tanggung jawab guard ini.
  }

  if (!session) {
    return {
      type: "redirect",
      to: `/login?error=access_required&next=${encodeURIComponent(pathname)}`,
    };
  }

  const allowedRoles = SEGMENT_ROLES[segment];
  if (!allowedRoles.includes(session.role)) {
    return { type: "redirect", to: roleHome(session.role) };
  }

  return { type: "allow" };
}
