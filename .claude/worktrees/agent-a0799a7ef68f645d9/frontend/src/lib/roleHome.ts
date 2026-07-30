import type { Role } from "./session";

/** VOK-H2-E2 §lib/roleHome.ts — satu pemetaan role→home. Dipakai proxy.ts (redirect role salah) & login. */
const HOME_BY_ROLE: Partial<Record<Role, "/sa" | "/app" | "/mentor" | "/student">> = {
  SuperAdmin: "/sa",
  TenantAdmin: "/app",
  DeptHead: "/app",
  Teacher: "/app",
  IndustryMentor: "/mentor",
  Student: "/student",
  // ParentViewer sengaja TIDAK dipetakan — belum ada dashboard terautentikasi utk role ini;
  // akses portofolio/sertifikat lewat link publik /p/[slug] & /verify/[code] (lihat lib/guard.ts).
};

/**
 * roleHome(role) → '/sa'|'/app'|'/mentor'|'/student' bila role py dashboard; '/login' bila
 * tidak (ParentViewer, atau role masa depan yg belum dipetakan) — fallback aman, bukan crash.
 */
export function roleHome(role: Role): "/sa" | "/app" | "/mentor" | "/student" | "/login" {
  return HOME_BY_ROLE[role] ?? "/login";
}
