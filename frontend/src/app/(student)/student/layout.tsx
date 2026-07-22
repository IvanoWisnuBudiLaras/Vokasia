import type { ReactNode } from "react";
import { LogoutButton } from "@/components/LogoutButton";
import { NotificationBell } from "@/components/NotificationBell";

const NAV = [
  { href: "/student", label: "Hari Ini", icon: "📓" },
  { href: "/student/history", label: "Riwayat", icon: "🗓️" },
  { href: "/student/portofolio", label: "Portofolio", icon: "🎓" },
];

/** Shell mobile-first Siswa PWA (PRD W1) — bottom nav, target sentuh >=44px, low-data. */
export default function StudentLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex items-center justify-between border-b border-border p-4">
        <span className="text-lg font-semibold text-ink">Vokasia</span>
        <div className="flex items-center gap-3">
          {/* VOK-H4-E2: placeholder 🔔 statis (H1-E2) diganti NotificationBell nyata (poll+badge+panel). */}
          <NotificationBell />
          <LogoutButton />
        </div>
      </header>
      <main className="flex-1 p-4 pb-20">{children}</main>
      <nav className="fixed inset-x-0 bottom-0 flex border-t border-border bg-surface">
        {NAV.map((item) => (
          <a
            key={item.href}
            href={item.href}
            className="flex h-[var(--tap-min)] flex-1 flex-col items-center justify-center gap-0.5 text-xs text-ink-muted"
          >
            <span aria-hidden="true">{item.icon}</span>
            {item.label}
          </a>
        ))}
      </nav>
    </div>
  );
}
