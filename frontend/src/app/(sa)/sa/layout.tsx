import type { ReactNode } from "react";
import { LogoutButton } from "@/components/LogoutButton";
import { NotificationBell } from "@/components/NotificationBell";

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
      <aside className="hidden w-56 shrink-0 flex-col border-r border-border bg-surface-muted p-4 md:flex">
        <div className="mb-6 flex items-center justify-between">
          <span className="text-lg font-semibold text-ink">Vokasia · SA</span>
          <NotificationBell />
        </div>
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
        <div className="mt-auto pt-4">
          <LogoutButton />
        </div>
      </aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
