import type { ReactNode } from "react";
import { LogoutButton } from "@/components/LogoutButton";
import { NotificationBell } from "@/components/NotificationBell";

const NAV = [
  { href: "/mentor", label: "Approve", icon: "✅" },
  { href: "/mentor/nilai", label: "Nilai", icon: "📝" },
];

/**
 * Shell mobile-first Mentor DUDI (PRD W2) — bottom nav, target sentuh >=44px.
 * data-theme="sekolah" (DECISIONS.md D20) — sama dgn shell Siswa, konsisten lintas role guru/murid.
 */
export default function MentorLayout({ children }: { children: ReactNode }) {
  return (
    <div data-theme="sekolah" className="flex min-h-screen flex-col bg-surface">
      <header className="relative flex items-center justify-between border-b border-border bg-surface p-4">
        <span className="text-lg font-semibold text-ink">Vokasia · Mentor</span>
        <div className="flex items-center gap-2">
          <NotificationBell />
          <LogoutButton />
        </div>
        <span
          aria-hidden="true"
          className="absolute inset-x-0 bottom-0 h-[3px] bg-gradient-to-r from-accent-bright to-accent-light"
        />
      </header>
      <main className="flex-1 p-4 pb-20">{children}</main>
      <nav className="fixed inset-x-0 bottom-0 flex border-t border-border bg-surface">
        {NAV.map((item) => (
          <a
            key={item.href}
            href={item.href}
            className="flex h-[var(--tap-min)] flex-1 flex-col items-center justify-center gap-0.5 text-xs text-ink-muted transition-colors hover:bg-primary-muted hover:text-ink focus-visible:bg-primary-muted"
          >
            <span aria-hidden="true">{item.icon}</span>
            {item.label}
          </a>
        ))}
      </nav>
    </div>
  );
}
