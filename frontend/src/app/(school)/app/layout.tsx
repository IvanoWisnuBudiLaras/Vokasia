import type { ReactNode } from "react";
import { ImpersonationBanner } from "@/components/ImpersonationBanner";
import { LogoutButton } from "@/components/LogoutButton";
import { NotificationBell } from "@/components/NotificationBell";
import { RoleMobileNav } from "@/components/RoleMobileNav";
import { SidebarNav, schoolNavForRole } from "@/components/SidebarNav";
import { getSession } from "@/lib/session";

/** Workspace sekolah: rail desktop dan navigasi tugas mobile memakai sumber item sama. */
export default async function SchoolLayout({ children }: { children: ReactNode }) {
  const session = await getSession();
  const navItems = schoolNavForRole(session?.role);
  return (
    <>
      <ImpersonationBanner />
      <div className="min-h-screen bg-surface-muted">
        <div className="mx-auto flex min-h-screen max-w-[1600px] bg-surface">
        <aside className="sticky top-0 hidden h-screen w-64 shrink-0 flex-col border-r border-border bg-surface-muted p-4 lg:flex">
          <div className="mb-8 flex items-start justify-between gap-3 px-2">
            <div>
              <span className="block text-lg font-bold tracking-tight text-ink">Vokasia</span>
              <span className="block text-xs text-ink-muted">Workspace sekolah</span>
            </div>
            <NotificationBell panelAlign="left" />
          </div>
          <SidebarNav items={navItems} />
          <div className="mt-auto border-t border-border pt-4">
            <LogoutButton />
          </div>
        </aside>
        <div className="flex min-w-0 flex-1 flex-col">
          <header className="flex items-center justify-between border-b border-border bg-surface px-4 py-3 lg:hidden">
            <div>
              <span className="block text-base font-bold text-ink">Vokasia</span>
              <span className="block text-xs text-ink-muted">Workspace sekolah</span>
            </div>
            <div className="flex items-center gap-1">
              <NotificationBell />
              <LogoutButton />
            </div>
          </header>
          <main className="min-w-0 flex-1 px-4 py-5 pb-24 sm:px-6 lg:p-8 lg:pb-8">{children}</main>
        </div>
        <RoleMobileNav items={navItems} hideAtDesktop />
        </div>
      </div>
    </>
  );
}
