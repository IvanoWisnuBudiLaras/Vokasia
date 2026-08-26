import type { ReactNode } from "react";
import { redirect } from "next/navigation";
import { ImpersonationBanner } from "@/components/ImpersonationBanner";
import { LogoutButton } from "@/components/LogoutButton";
import { NotificationBell } from "@/components/NotificationBell";
import { RoleMobileNav } from "@/components/RoleMobileNav";
import { WorkspaceSidebar, type WorkspaceNavItem } from "@/components/WorkspaceSidebar";
import { getVerifiedSession } from "@/lib/serverSession";
const NAV: WorkspaceNavItem[] = [
  { href: "/sa", label: "Ringkasan", icon: "layout-dashboard" },
  { href: "/sa/tenants", label: "Tenant", icon: "building-2" },
  { href: "/sa/dudi", label: "DUDI", icon: "briefcase-business" },
  { href: "/sa/students", label: "Siswa", icon: "graduation-cap" },
  { href: "/sa/plans", label: "Paket", icon: "package" },
  { href: "/sa/invoices", label: "Invoice", icon: "receipt" },
  { href: "/sa/audit", label: "Audit", icon: "list-checks" },
];

/** Workspace SuperAdmin mempertahankan data density, dengan nav mobile yang tetap dapat diakses. */
export default async function SuperAdminLayout({ children }: { children: ReactNode }) {
  const session = await getVerifiedSession();
  if (!session || session.role !== "SuperAdmin") {
    redirect("/login?error=access_required");
  }
  return (
    <>
      <ImpersonationBanner />
      <div className="min-h-screen bg-surface-paper selection:bg-brand-soft selection:text-ink">
        <div className="mx-auto flex min-h-screen max-w-[1600px]">
        <aside className="sticky top-0 hidden h-screen w-64 shrink-0 flex-col border-r border-border/40 bg-surface-paper p-5 lg:flex">
          <div className="mb-8 flex items-start justify-between gap-3 px-2">
            <div>
              <span className="block text-xl font-bold tracking-tight text-ink">Vokasia</span>
              <span className="block text-xs font-medium text-ink-muted">Operasi platform</span>
            </div>
            <NotificationBell panelAlign="left" />
          </div>
          <WorkspaceSidebar ariaLabel="Navigasi SuperAdmin" items={NAV} />
          <div className="mt-auto border-t border-border/40 pt-4">
            <LogoutButton />
          </div>
        </aside>
        <div className="flex min-w-0 flex-1 flex-col bg-surface">
          <header className="flex items-center justify-between border-b border-border/40 bg-surface px-4 py-3 lg:hidden">
            <div>
              <span className="block text-base font-bold text-ink">Vokasia</span>
              <span className="block text-xs text-ink-muted">Operasi platform</span>
            </div>
            <div className="flex items-center gap-1">
              <NotificationBell />
              <LogoutButton />
            </div>
          </header>
          <main className="min-w-0 flex-1 px-4 py-6 pb-24 sm:px-8 lg:p-10 lg:pb-10">{children}</main>
        </div>
        <RoleMobileNav items={NAV} hideAtDesktop />
        </div>
      </div>
    </>
  );
}
