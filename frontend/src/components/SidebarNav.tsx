import { WorkspaceSidebar, type WorkspaceNavItem } from "@/components/WorkspaceSidebar";
import { getSession } from "@/lib/session";

const NAV: WorkspaceNavItem[] = [
  { href: "/app", label: "Ringkasan", icon: "layout-dashboard" },
  { href: "/app/bimbingan", label: "Bimbingan", icon: "message-square-text" },
  { href: "/app/penilaian", label: "Penilaian", icon: "clipboard-check" },
  { href: "/app/billing", label: "Billing", icon: "receipt" },
];

const BILLING_ROLES = new Set(["TenantAdmin"]);

export function schoolNavForRole(role?: string): WorkspaceNavItem[] {
  return BILLING_ROLES.has(role ?? "") ? NAV : NAV.filter((item) => item.href !== "/app/billing");
}

/** Navigasi sekolah hanya menampilkan alur yang sudah punya layar fungsional. */
export async function SidebarNav({ items }: { items?: WorkspaceNavItem[] } = {}) {
  const session = await getSession();
  return <WorkspaceSidebar ariaLabel="Navigasi sekolah" items={items ?? schoolNavForRole(session?.role)} />;
}

export const SCHOOL_MOBILE_NAV = NAV;
