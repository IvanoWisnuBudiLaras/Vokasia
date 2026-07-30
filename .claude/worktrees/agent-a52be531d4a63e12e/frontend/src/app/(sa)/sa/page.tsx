import { Card, ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { HealthDto, KpiDto } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

function HealthRow({ label, value, warnBelow }: { label: string; value: number | null; warnBelow?: (v: number) => boolean }) {
  const unknown = value === null;
  const warn = !unknown && warnBelow ? warnBelow(value) : false;
  return (
    <div className="flex items-center justify-between border-b border-border py-2 text-sm last:border-0">
      <span className="text-ink-muted">{label}</span>
      <span className={warn ? "font-medium text-status-amber" : "font-medium text-ink"}>
        {unknown ? "— (tak tersedia)" : value} {warn ? "⚠" : unknown ? "" : "✅"}
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
      <div>
        <h1 className="text-xl font-semibold text-ink">Dashboard Platform</h1>
        <p className="text-sm text-ink-muted">Ringkasan lintas seluruh tenant Vokasia.</p>
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
        <Card title="Tenant Aktif"><span className="text-2xl font-semibold text-ink">{kpi.activeTenants}</span></Card>
        <Card title="Siswa Aktif"><span className="text-2xl font-semibold text-ink">{kpi.activeStudents}</span></Card>
        <Card title="Jurnal Hari Ini"><span className="text-2xl font-semibold text-ink">{kpi.journalsToday}</span></Card>
        <Card title="Tingkat Isi Jurnal"><span className="text-2xl font-semibold text-ink">{kpi.journalFillRate.toFixed(1)}%</span></Card>
        <Card title="MRR"><span className="text-2xl font-semibold text-ink">Rp {kpi.mrr.toLocaleString("id-ID")}</span></Card>
      </div>

      <Card title="System Health">
        <HealthRow label="Queue Depth (RabbitMQ)" value={health.queueDepth} warnBelow={(v) => v > 100} />
        <HealthRow label="Dead-letter (fault queue)" value={health.dlqCount} warnBelow={(v) => v > 0} />
        <HealthRow label="Failed Jobs (Hangfire)" value={health.failedJobs} warnBelow={(v) => v > 0} />
        <HealthRow label="Outbox Belum Terpublish" value={health.outboxUnpublished} warnBelow={(v) => v > 50} />
        <HealthRow label="API p95 (ms)" value={health.apiP95Ms} />
        <HealthRow label="Disk Terpakai (%)" value={health.diskPct} warnBelow={(v) => v > 80} />
      </Card>
    </div>
  );
}
