import type { ReactNode } from "react";
import { redirect } from "next/navigation";
import { ImpersonationBanner } from "@/components/ImpersonationBanner";
import { LogoutButton } from "@/components/LogoutButton";
import { MobileAccountMenu } from "@/components/MobileAccountMenu";
import { NotificationBell } from "@/components/NotificationBell";
import { RoleMobileNav, type RoleMobileNavItem } from "@/components/RoleMobileNav";
import { WorkspaceSidebar } from "@/components/WorkspaceSidebar";
import { getVerifiedSession } from "@/lib/serverSession";
const NAV: RoleMobileNavItem[] = [
  { href: "/mentor", label: "Approval", icon: "clipboard-check" },
  { href: "/mentor/nilai", label: "Penilaian", icon: "file-pen-line" },
];

/**
 * Shell mobile-first Mentor DUDI (PRD W2) — bottom nav, target sentuh >=44px.
 * data-theme="sekolah" (DECISIONS.md D20) — sama dgn shell Siswa, konsisten lintas role guru/murid.
 */
export default async function MentorLayout({ children }: { children: ReactNode }) {
  const session = await getVerifiedSession();
  if (!session || session.role !== "IndustryMentor") {
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
              <span className="block text-xs font-medium text-ink-muted">Ruang mentor industri</span>
            </div>
            <NotificationBell panelAlign="left" />
          </div>
          <WorkspaceSidebar ariaLabel="Navigasi mentor" items={NAV} />
          <div className="mt-auto border-t border-border/40 pt-4"><LogoutButton /></div>
        </aside>
        <div className="flex min-w-0 flex-1 flex-col bg-surface">
        <header className="relative flex items-center justify-between border-b border-border/40 bg-surface px-4 py-3 lg:hidden">
          <div>
            <span className="block text-lg font-bold tracking-tight text-ink">Vokasia</span>
            <span className="block text-xs text-ink-muted">Ruang mentor industri</span>
          </div>
          <div className="flex items-center gap-1">
            <NotificationBell />
            <MobileAccountMenu />
          </div>
        </header>
        <main className="flex-1 px-4 py-6 pb-24 sm:px-6 lg:p-10 lg:pb-10">{children}</main>
        </div>
        <RoleMobileNav items={NAV} hideAtDesktop />
        </div>
      </div>
    </>
  );
}
