import type { ReactNode } from "react";

const NAV = [
  { href: "/mentor", label: "Approve", icon: "✅" },
  { href: "/mentor/nilai", label: "Nilai", icon: "📝" },
];

/** Shell mobile-first Mentor DUDI (PRD W2) — bottom nav, target sentuh >=44px. */
export default function MentorLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen flex-col">
      <header className="border-b border-border p-4">
        <span className="text-lg font-semibold text-ink">Vokasia · Mentor</span>
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
