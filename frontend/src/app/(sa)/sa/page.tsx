import { PageHeading } from "@/components/PageHeading";
import { ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { HealthDto, KpiDto } from "@/lib/apiTypes";
import { DashboardCharts } from "./DashboardCharts";

export const dynamic = "force-dynamic";

function HealthRow({ label, value, warnBelow }: { label: string; value: number | null; warnBelow?: (v: number) => boolean }) {
  const unknown = value === null;
  const warn = !unknown && warnBelow ? warnBelow(value) : false;
  return (
    <div className="flex items-center justify-between border-b border-border py-2 text-sm last:border-0">
      <span className="text-ink-muted">{label}</span>
      <span className={`inline-flex items-center gap-1.5 ${warn ? "font-medium text-status-amber" : "font-medium text-ink"}`}>
        {unknown ? "— (tak tersedia)" : value}
        {!unknown && <Icon name={warn ? "warning" : "check"} size={16} />}
      </span>
    </div>
  );
}

/**
 * VOK-H6-E2 §1 sa/page.tsx — KPI cards (GetPlatformKpis) + panel SYSTEM HEALTH (GetSystemHealth).
 * `QueueDepth`/`DlqCount`/`FailedJobs`/`ApiP95Ms`/`DiskPct` BOLEH null (backend best-effort — lihat
 * doc-comment SaOpsEndpoints.GetSystemHealth: RabbitMQ mgmt API/Hangfire/APM mungkin tak terjangkau
 * di lingkungan tertentu) — ditampilkan "— (tak tersedia)" alih-alih angka palsu 0.
 */
export default async function SuperAdminHomePage() {
  let kpi: KpiDto | null = null;
  let health: HealthDto | null = null;
  let loadError = false;

  try {
    [kpi, health] = await Promise.all([fetcher<KpiDto>("/sa/kpis"), fetcher<HealthDto>("/sa/health")]);
  } catch (err) {
    console.error("[sa] gagal memuat KPI/health:", err);
    loadError = true;
  }

  if (loadError || !kpi || !health) {
    return <ErrorState message="KPI & system health belum bisa dimuat." />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeading eyebrow="OPERASI PLATFORM" title="Apa yang perlu ditindaklanjuti?" description="Mulai dari tenant, penagihan, perusahaan, lalu audit dan kesehatan layanan." />

      <section aria-labelledby="platform-snapshot" className="border-y border-border py-3">
        <h2 id="platform-snapshot" className="mb-3 text-sm font-semibold text-ink">Snapshot platform</h2>
        <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm md:grid-cols-5">
          <div><dt className="text-ink-muted">Tenant aktif</dt><dd className="font-semibold tabular-nums text-ink">{kpi.activeTenants}</dd></div>
          <div><dt className="text-ink-muted">Siswa aktif</dt><dd className="font-semibold tabular-nums text-ink">{kpi.activeStudents}</dd></div>
          <div><dt className="text-ink-muted">Jurnal hari ini</dt><dd className="font-semibold tabular-nums text-ink">{kpi.journalsToday}</dd></div>
          <div><dt className="text-ink-muted">Isi jurnal</dt><dd className="font-semibold tabular-nums text-ink">{kpi.journalFillRate.toFixed(1)}%</dd></div>
          <div><dt className="text-ink-muted">MRR</dt><dd className="font-semibold tabular-nums text-ink">Rp {kpi.mrr.toLocaleString("id-ID")}</dd></div>
        </dl>
      </section>

      <DashboardCharts />

      <section aria-labelledby="system-health" className="border-y border-border py-3">
        <h2 id="system-health" className="mb-2 text-sm font-semibold text-ink">System health</h2>
        <HealthRow label="Queue Depth (RabbitMQ)" value={health.queueDepth} warnBelow={(v) => v > 100} />
        <HealthRow label="Dead-letter (fault queue)" value={health.dlqCount} warnBelow={(v) => v > 0} />
        <HealthRow label="Failed Jobs (Hangfire)" value={health.failedJobs} warnBelow={(v) => v > 0} />
        <HealthRow label="Outbox Belum Terpublish" value={health.outboxUnpublished} warnBelow={(v) => v > 50} />
        <HealthRow label="API p95 (ms)" value={health.apiP95Ms} />
        <HealthRow label="Disk Terpakai (%)" value={health.diskPct} warnBelow={(v) => v > 80} />
      </section>
    </div>
  );
}
