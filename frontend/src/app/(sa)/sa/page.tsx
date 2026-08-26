import Link from "next/link";
import type { ReactNode } from "react";
import { ErrorState, StatusBadge } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { AuditDto, HealthDto, InvoiceDto, KpiDto, Paged, TenantDto } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

const monthStart = new Date(new Date().getFullYear(), new Date().getMonth(), 1);

function Section({ title, description, children, className = "" }: { title: string; description?: string; children: ReactNode; className?: string }) {
  return <section className={`border-t border-border pt-4 ${className}`}><div className="mb-3 flex flex-wrap items-baseline justify-between gap-2"><h2 className="text-lg font-semibold text-ink">{title}</h2>{description && <p className="text-sm text-ink-muted">{description}</p>}</div>{children}</section>;
}

function MetricLine({ label, value, tone = "neutral" }: { label: string; value: string; tone?: "neutral" | "amber" | "red" | "green" }) {
  const toneClass = tone === "red" ? "text-status-red" : tone === "amber" ? "text-status-amber" : tone === "green" ? "text-status-green" : "text-ink";
  return <div className="flex items-center justify-between gap-4 border-b border-border/70 py-2 last:border-0"><span className="text-sm text-ink-muted">{label}</span><span className={`text-sm font-medium ${toneClass}`}>{value}</span></div>;
}

export default async function SuperAdminHomePage() {
  let kpi: KpiDto | null = null;
  let health: HealthDto | null = null;
  let tenants: Paged<TenantDto> | null = null;
  let invoices: InvoiceDto[] = [];
  let audit: Paged<AuditDto> | null = null;
  try {
    [kpi, health, tenants, invoices, audit] = await Promise.all([
      fetcher<KpiDto>("/sa/kpis"),
      fetcher<HealthDto>("/sa/health"),
      fetcher<Paged<TenantDto>>("/sa/tenants?pageSize=200"),
      fetcher<InvoiceDto[]>("/sa/invoices"),
      fetcher<Paged<AuditDto>>("/sa/audit-logs?page=1&pageSize=8"),
    ]);
  } catch (err) {
    console.error("[sa] gagal memuat ringkasan:", err);
    return <ErrorState message="Ringkasan platform belum bisa dimuat." />;
  }

  if (!kpi || !health || !tenants || !audit) return <ErrorState message="Ringkasan platform belum bisa dimuat." />;

  const overdueInvoices = invoices.filter((invoice) => invoice.status === 0 && new Date(invoice.periodMonth) < monthStart);
  const pendingProof = invoices.filter((invoice) => invoice.status === 1);
  const problems = [
    health.dlqCount && health.dlqCount > 0 ? { title: `${health.dlqCount} pesan masuk fault queue`, detail: "Periksa consumer yang gagal diproses.", href: "/sa", tone: "red" as const } : null,
    health.failedJobs && health.failedJobs > 0 ? { title: `${health.failedJobs} job Hangfire gagal`, detail: "Tinjau pekerjaan terjadwal yang gagal.", href: "/sa", tone: "red" as const } : null,
    overdueInvoices.length > 0 ? { title: `${overdueInvoices.length} invoice lewat jatuh tempo`, detail: "Buka daftar invoice untuk memeriksa status pembayaran.", href: "/sa/invoices", tone: "amber" as const } : null,
    pendingProof.length > 0 ? { title: `${pendingProof.length} bukti pembayaran menunggu konfirmasi`, detail: "Konfirmasi setelah bukti transfer diperiksa.", href: "/sa/invoices", tone: "amber" as const } : null,
    health.queueDepth && health.queueDepth > 100 ? { title: `Queue memiliki ${health.queueDepth} pesan`, detail: "Periksa antrean sebelum backlog bertambah.", href: "/sa", tone: "amber" as const } : null,
  ].filter((item): item is NonNullable<typeof item> => item !== null);

  const tenantIssues = new Map<string, string[]>();
  for (const tenant of tenants.items) if (!tenant.isActive) tenantIssues.set(tenant.id, ["Tenant nonaktif"]);
  for (const invoice of [...overdueInvoices, ...pendingProof]) {
    const tenant = tenants.items.find((item) => item.id === invoice.tenantId);
    if (!tenant) continue;
    const issues = tenantIssues.get(tenant.id) ?? [];
    const issue = invoice.status === 1 ? "Bukti pembayaran menunggu konfirmasi" : "Invoice lewat jatuh tempo";
    if (!issues.includes(issue)) issues.push(issue);
    tenantIssues.set(tenant.id, issues);
  }
  const attention = tenants.items.filter((tenant) => tenantIssues.has(tenant.id)).slice(0, 8);

  return <div className="flex max-w-[1400px] flex-col gap-8">
    <header className="border-b border-border pb-5"><h1 className="text-3xl font-bold tracking-tight text-ink">Ringkasan platform</h1><p className="mt-1 max-w-2xl text-base leading-6 text-ink-muted">Prioritaskan masalah layanan, tenant, billing, dan keamanan akun yang membutuhkan tindakan.</p></header>

    <div className="grid gap-8 lg:grid-cols-[minmax(0,1.45fr)_minmax(280px,0.8fr)]">
      <Section title="Perlu tindakan" description={problems.length ? `${problems.length} temuan aktif` : "Tidak ada masalah aktif"}>
        {problems.length === 0 ? <p className="border-y border-border py-5 text-sm text-ink-muted">Semua layanan terukur berjalan normal dan tidak ada billing yang perlu ditinjau.</p> : <ul className="divide-y divide-border border-y border-border">{problems.map((problem) => <li key={problem.title} className="flex flex-wrap items-start justify-between gap-3 py-4"><div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><StatusBadge status={problem.tone} label={problem.tone === "red" ? "Kritis" : "Perlu perhatian"} /><h3 className="font-medium text-ink">{problem.title}</h3></div><p className="mt-1 text-sm text-ink-muted">{problem.detail}</p></div><Link href={problem.href} className="min-h-[44px] whitespace-nowrap pt-2 text-sm font-medium text-primary underline-offset-4 hover:underline focus-visible:outline-2 focus-visible:outline-focus">Tinjau →</Link></li>)}</ul>}
      </Section>
      <Section title="Kesehatan operasional" description="Data yang benar-benar terukur">
        <div className="border-y border-border"><MetricLine label="Queue RabbitMQ" value={health.queueDepth === null ? "Belum terukur" : `${health.queueDepth} pesan`} tone={health.queueDepth && health.queueDepth > 100 ? "amber" : "neutral"} /><MetricLine label="Fault queue" value={health.dlqCount === null ? "Belum terukur" : `${health.dlqCount} pesan`} tone={health.dlqCount && health.dlqCount > 0 ? "red" : "green"} /><MetricLine label="Job Hangfire gagal" value={health.failedJobs === null ? "Belum terukur" : String(health.failedJobs)} tone={health.failedJobs && health.failedJobs > 0 ? "red" : "green"} /><MetricLine label="Outbox belum terbit" value={String(health.outboxUnpublished)} tone={health.outboxUnpublished > 50 ? "amber" : "neutral"} /></div>
        <p className="mt-3 text-xs text-ink-muted">Menampilkan sinyal operasional yang tersedia saat ini.</p>
      </Section>
    </div>

    <Section title="Tenant yang perlu perhatian" description="Status dan masalah yang punya tindakan jelas">
      {attention.length === 0 ? <p className="border-y border-border py-5 text-sm text-ink-muted">Tidak ada tenant yang memerlukan tindakan.</p> : <div className="overflow-x-auto border-y border-border"><table className="w-full min-w-[680px] text-left text-sm"><thead className="text-ink-muted"><tr><th className="py-3 pr-4 font-medium">Tenant</th><th className="py-3 pr-4 font-medium">Status</th><th className="py-3 pr-4 font-medium">Masalah utama</th><th className="py-3 font-medium">Aksi</th></tr></thead><tbody className="divide-y divide-border">{attention.map((tenant) => { const issues = tenantIssues.get(tenant.id) ?? []; return <tr key={tenant.id}><td className="py-3 pr-4 font-medium text-ink">{tenant.schoolName}<span className="ml-2 text-xs font-normal text-ink-muted">{tenant.city ?? ""}</span></td><td className="py-3 pr-4"><StatusBadge status={tenant.isActive ? "amber" : "red"} label={tenant.isActive ? "Aktif" : "Nonaktif"} /></td><td className="py-3 pr-4 text-ink-muted">{issues[0]}{issues.length > 1 && <span className="ml-1">+{issues.length - 1} masalah lain</span>}</td><td className="py-3"><Link href={`/sa/tenants/${tenant.id}`} className="font-medium text-primary underline-offset-4 hover:underline">Buka tenant</Link></td></tr>; })}</tbody></table></div>}
    </Section>

    <div className="grid gap-8 lg:grid-cols-3">
      <Section title="Billing" description={`${invoices.length} invoice tercatat`}><div className="border-y border-border"><MetricLine label="Lewat jatuh tempo" value={String(overdueInvoices.length)} tone={overdueInvoices.length ? "red" : "green"} /><MetricLine label="Menunggu konfirmasi" value={String(pendingProof.length)} tone={pendingProof.length ? "amber" : "green"} /></div><Link href="/sa/invoices" className="mt-3 inline-block min-h-[44px] pt-2 text-sm font-medium text-primary underline-offset-4 hover:underline">Lihat billing →</Link></Section>
      <Section title="Platform" description="Snapshot saat ini"><div className="border-y border-border"><MetricLine label="Tenant aktif" value={String(kpi.activeTenants)} /><MetricLine label="Siswa dengan placement aktif" value={String(kpi.activeStudents)} /><MetricLine label="Jurnal hari ini" value={String(kpi.journalsToday)} /><MetricLine label="MRR" value={`Rp ${kpi.mrr.toLocaleString("id-ID")}`} /></div></Section>
      <Section title="Aktivitas penting" description="8 event terbaru"><ul className="divide-y divide-border border-y border-border">{audit.items.slice(0, 5).map((event) => <li key={event.id} className="py-3"><p className="text-sm font-medium text-ink">{event.action}</p><p className="mt-1 text-xs text-ink-muted">{event.entity} · {new Date(event.createdAt).toLocaleString("id-ID")}</p></li>)}{audit.items.length === 0 && <li className="py-4 text-sm text-ink-muted">Belum ada aktivitas penting.</li>}</ul><Link href="/sa/audit" className="mt-3 inline-block min-h-[44px] pt-2 text-sm font-medium text-primary underline-offset-4 hover:underline">Lihat audit lengkap →</Link></Section>
    </div>
  </div>;
}
