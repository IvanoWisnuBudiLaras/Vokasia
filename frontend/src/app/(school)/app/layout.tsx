import type { ReactNode } from "react";

const NAV = [
  { href: "/app", label: "Dashboard" },
  { href: "/app/periode", label: "Periode" },
  { href: "/app/siswa", label: "Siswa" },
  { href: "/app/dudi", label: "DUDI" },
  { href: "/app/placement", label: "Placement" },
  { href: "/app/jurnal", label: "Jurnal" },
  { href: "/app/penilaian", label: "Penilaian" },
  { href: "/app/billing", label: "Billing" },
];

/** Shell desktop-first Admin Sekolah/Guru (PRD W3). */
export default function SchoolLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-56 shrink-0 border-r border-border bg-surface-muted p-4 md:block">
        <div className="mb-6 text-lg font-semibold text-ink">Vokasia</div>
        <nav className="flex flex-col gap-1">
          {NAV.map((item) => (
            <a
              key={item.href}
              href={item.href}
              className="rounded-[var(--radius-sm)] px-3 py-2 text-sm text-ink-muted hover:bg-surface hover:text-ink"
            >
              {item.label}
            </a>
          ))}
        </nav>
      </aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
