import Link from "next/link";
import { MaterialIcon } from "@/components/ui/MaterialIcon";

/** Platform actions with a clear destination; avoids fabricated analytics. */
export function DashboardCharts() {
  const actions = [
    ["Kelola tenant", "Provisioning, admin awal, dan status tenant.", "/sa/tenants", "school"],
    ["Tinjau invoice", "Periksa bukti pembayaran dan status penagihan.", "/sa/invoices", "billing"],
    ["Operasi perusahaan", "Kelola katalog DUDI lintas tenant.", "/sa/dudi", "company"],
    ["Audit aktivitas", "Telusuri tindakan sensitif dan perubahan platform.", "/sa/audit", "audit"],
  ] as const;

  return (
    <section aria-labelledby="platform-actions">
      <div className="mb-2 flex items-baseline justify-between">
        <h2 id="platform-actions" className="text-sm font-semibold text-ink">Tindakan platform</h2>
        <span className="text-xs text-ink-muted">Pilih area yang perlu ditindaklanjuti</span>
      </div>
      <div className="divide-y divide-border border-y border-border">
        {actions.map(([title, description, href, icon]) => (
          <Link key={href} href={href} className="flex min-h-16 items-center gap-3 py-3 hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus">
            <MaterialIcon name={icon} decorative />
            <span className="flex-1">
              <span className="block text-sm font-medium text-ink">{title}</span>
              <span className="block text-xs text-ink-muted">{description}</span>
            </span>
            <span aria-hidden="true" className="text-ink-muted">›</span>
          </Link>
        ))}
      </div>
    </section>
  );
}
