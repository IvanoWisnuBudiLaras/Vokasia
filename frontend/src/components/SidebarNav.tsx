import { WorkspaceSidebar, type WorkspaceNavItem } from "@/components/WorkspaceSidebar";
import { getSession } from "@/lib/session";

const NAV: WorkspaceNavItem[] = [
  { href: "/app", label: "Ringkasan", icon: "layout-dashboard" },
  { href: "/app/siswa", label: "Siswa", icon: "graduation-cap" },
  { href: "/app/bimbingan", label: "Bimbingan", icon: "message-square-text" },
  { href: "/app/penilaian", label: "Penilaian", icon: "clipboard-check" },
  { href: "/app/perkembangan", label: "Perkembangan", icon: "list-checks" },
  { href: "/app/penilaian/rubrik", label: "Rubrik", icon: "file-pen-line" },
  { href: "/app/laporan/perkembangan", label: "Laporan", icon: "file-text" },
  { href: "/app/operasi", label: "Operasi", icon: "list-checks" },
  { href: "/app/billing", label: "Billing", icon: "receipt" },
];

const BILLING_ROLES = new Set(["TenantAdmin"]);
const OPERATIONS_ROLES = new Set(["TenantAdmin", "DeptHead"]);

export function schoolNavForRole(role?: string): WorkspaceNavItem[] {
  return NAV.filter((item) => {
    if (item.href === "/app/siswa") return role === "Teacher";
    if (item.href === "/app/billing") return BILLING_ROLES.has(role ?? "");
    if (item.href === "/app/operasi") return OPERATIONS_ROLES.has(role ?? "");
    if (item.href === "/app/penilaian/rubrik") return role === "TenantAdmin";
    return true;
  });
}

/** Navigasi sekolah hanya menampilkan alur yang sudah punya layar fungsional. */
export async function SidebarNav({ items }: { items?: WorkspaceNavItem[] } = {}) {
  const session = await getSession();
  return <WorkspaceSidebar ariaLabel="Navigasi sekolah" items={items ?? schoolNavForRole(session?.role)} />;
}

export const SCHOOL_MOBILE_NAV = NAV;
