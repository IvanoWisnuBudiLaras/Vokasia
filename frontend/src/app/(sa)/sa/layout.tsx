import type { ReactNode } from "react";

const NAV = [
  { href: "/sa", label: "KPI" },
  { href: "/sa/tenants", label: "Tenants" },
  { href: "/sa/dudi", label: "DUDI Registry" },
  { href: "/sa/plans", label: "Plans" },
  { href: "/sa/invoices", label: "Invoices" },
  { href: "/sa/audit", label: "Audit" },
];

/** Shell desktop-first Superadmin (PRD W5) — sidebar tetap, konten scrollable. */
export default function SuperAdminLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-56 shrink-0 border-r border-border bg-surface-muted p-4 md:block">
        <div className="mb-6 text-lg font-semibold text-ink">Vokasia · SA</div>
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
