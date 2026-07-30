import type { ReactNode } from "react";
import { ImpersonationBanner } from "@/components/ImpersonationBanner";
import { LogoutButton } from "@/components/LogoutButton";
import { NotificationBell } from "@/components/NotificationBell";
import { RoleMobileNav, type RoleMobileNavItem } from "@/components/RoleMobileNav";
import { WorkspaceSidebar } from "@/components/WorkspaceSidebar";

const NAV: RoleMobileNavItem[] = [
  { href: "/student", label: "Hari Ini", icon: "notebook-pen" },
  { href: "/student/history", label: "Riwayat", icon: "calendar-days" },
  { href: "/student/portofolio", label: "Portofolio", icon: "graduation-cap" },
];

/**
 * Shell mobile-first Siswa PWA (PRD W1) — bottom nav, target sentuh >=44px, low-data.
 * data-theme="sekolah" (DECISIONS.md D20): palet cyan-terang (brief guru/murid), CSS-only,
 * tanpa asset tambahan — tetap dalam anggaran 3G (NFR-PERF-05).
 */
export default function StudentLayout({ children }: { children: ReactNode }) {
  return (
    <>
      <ImpersonationBanner />
      <div data-theme="sekolah" className="min-h-screen bg-surface-muted">
        <div className="mx-auto flex min-h-screen max-w-[1600px] bg-surface">
        <aside className="sticky top-0 hidden h-screen w-64 shrink-0 flex-col border-r border-border bg-surface-muted p-4 lg:flex">
          <div className="mb-8 flex items-start justify-between gap-3 px-2">
            <div>
              <span className="block text-lg font-bold tracking-tight text-ink">Vokasia</span>
              <span className="block text-xs text-ink-muted">Ruang belajar PKL</span>
            </div>
            <NotificationBell panelAlign="left" />
          </div>
          <WorkspaceSidebar ariaLabel="Navigasi siswa" items={NAV} />
          <div className="mt-auto border-t border-border pt-4"><LogoutButton /></div>
        </aside>
        <div className="flex min-w-0 flex-1 flex-col">
        <header className="relative flex items-center justify-between border-b border-border bg-surface px-4 py-3 lg:hidden">
          <div>
            <span className="block text-lg font-bold tracking-tight text-ink">Vokasia</span>
            <span className="block text-xs text-ink-muted">Ruang belajar PKL</span>
          </div>
          <div className="flex items-center gap-1">
            <NotificationBell />
            <LogoutButton />
          </div>
          <span aria-hidden="true" className="absolute inset-x-0 bottom-0 h-1 bg-accent-bright" />
        </header>
        <main className="flex-1 px-4 py-5 pb-24 lg:p-8 lg:pb-8">{children}</main>
        </div>
        <RoleMobileNav items={NAV} hideAtDesktop />
        </div>
      </div>
    </>
  );
}
