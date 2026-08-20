import Link from "next/link";
import { MaterialIcon } from "@/components/ui/MaterialIcon";

export interface KpiCardsProps {
  journalTodayPct: number;
  pendingApprovals: number;
  lateVisits: number;
  flaggedCount: number;
}

/** Compact operational index; collections remain lists rather than KPI cards. */
export function KpiCards({ journalTodayPct, pendingApprovals, lateVisits, flaggedCount }: KpiCardsProps) {
  const rows = [
    { label: "Siswa perlu tindak lanjut", value: flaggedCount, href: "#siswa-bermasalah", icon: "warning" as const },
    { label: "Approval jurnal tertunda", value: pendingApprovals, href: "/app/bimbingan", icon: "journal" as const },
    { label: "Kunjungan terlambat", value: lateVisits, href: "/app/bimbingan", icon: "visit" as const },
    { label: "Pengisian jurnal hari ini", value: `${journalTodayPct.toFixed(1)}%`, href: "#siswa-bermasalah", icon: "verified" as const },
  ];

  return (
    <section aria-labelledby="operational-summary" className="border-y border-border">
      <h2 id="operational-summary" className="sr-only">Ringkasan operasi</h2>
      <div className="divide-y divide-border">
        {rows.map((row) => (
          <Link key={row.label} href={row.href} className="flex min-h-14 items-center gap-3 px-1 py-3 hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus">
            <MaterialIcon name={row.icon} decorative />
            <span className="flex-1 text-sm text-ink">{row.label}</span>
            <strong className={row.label === "Siswa perlu tindak lanjut" && flaggedCount > 0 ? "text-status-red" : "text-ink"}>{row.value}</strong>
            <span aria-hidden="true" className="text-ink-muted">›</span>
          </Link>
        ))}
      </div>
    </section>
  );
}
