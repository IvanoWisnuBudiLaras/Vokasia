import type { Role } from "./session";

/** VOK-H2-E2 §lib/roleHome.ts — satu pemetaan role→home. Dipakai proxy.ts (redirect role salah) & login. */
const HOME_BY_ROLE: Record<string, "/sa" | "/app" | "/mentor" | "/student"> = {
  SuperAdmin: "/sa",
  TenantAdmin: "/app",
  DeptHead: "/app",
  Teacher: "/app",
  IndustryMentor: "/mentor",
  Student: "/student",

  superadmin: "/sa",
  tenantadmin: "/app",
  depthead: "/app",
  teacher: "/app",
  industrymentor: "/mentor",
  student: "/student",

  "0": "/sa",
  "1": "/app",
  "2": "/app",
  "3": "/app",
  "4": "/mentor",
  "5": "/student",
};

/**
 * roleHome(role) → '/sa'|'/app'|'/mentor'|'/student' bila role py dashboard; '/login' bila
 * tidak — fallback aman.
 */
export function roleHome(role: Role | string): "/sa" | "/app" | "/mentor" | "/student" | "/login" {
  if (!role) return "/login";
  return HOME_BY_ROLE[role] ?? HOME_BY_ROLE[String(role).toLowerCase()] ?? "/login";
}
