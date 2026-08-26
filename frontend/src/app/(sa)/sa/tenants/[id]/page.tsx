import Link from "next/link";
import { ErrorState, StatusBadge } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { AuditDto, InvoiceDto, Paged, PlanDto, SaTenantUsageDto, SaUserDto, TenantDetailDto } from "@/lib/apiTypes";
import { TenantBillingTable } from "../TenantBillingTable";
import { TenantSummary } from "../TenantSummary";
import { TenantUsersTable } from "../TenantUsersTable";

export const dynamic = "force-dynamic";

type SearchParams = { tab?: string };

function AuditList({ items }: { items: AuditDto[] }) {
  return <div className="divide-y divide-border border-y border-border">{items.map((item) => <div key={item.id} className="py-3"><p className="text-sm font-medium text-ink">{item.action}</p><p className="mt-1 text-xs text-ink-muted">{item.entity} · {new Date(item.createdAt).toLocaleString("id-ID")}</p><p className="mt-1 break-all font-mono text-xs text-ink-muted">{item.metaJson}</p></div>)}{items.length === 0 && <p className="py-6 text-sm text-ink-muted">Belum ada aktivitas untuk tenant ini.</p>}</div>;
}

export default async function SaTenantDetailPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<SearchParams> }) {
  const { id } = await params;
  const { tab = "summary" } = await searchParams;
  let detail: TenantDetailDto;
  let plans: PlanDto[];
  let usage: SaTenantUsageDto;
  let invoices: InvoiceDto[];
  let users: SaUserDto[];
  let audit: Paged<AuditDto>;
  try {
    [detail, plans, usage, invoices, users, audit] = await Promise.all([
      fetcher<TenantDetailDto>(`/sa/tenants/${id}`),
      fetcher<PlanDto[]>("/sa/plans"),
      fetcher<SaTenantUsageDto>(`/sa/tenants/${id}/usage`),
      fetcher<InvoiceDto[]>(`/sa/invoices?tenantId=${id}`),
      fetcher<SaUserDto[]>(`/sa/tenants/${id}/users?active=true`),
      fetcher<Paged<AuditDto>>(`/sa/audit-logs?tenantId=${id}&page=1&pageSize=30`),
    ]);
  } catch (err) {
    console.error("[sa/tenant] gagal memuat detail:", err);
    return <ErrorState message="Detail tenant belum bisa dimuat." />;
  }

  const plan = plans.find((item) => item.id === detail.tenant.planId);
  const tabs = [["summary", "Ringkasan"], ["billing", "Billing"], ["users", "User"], ["usage", "Usage"], ["audit", "Audit"]] as const;
  return <div className="flex max-w-[1400px] flex-col gap-6"><div className="flex flex-wrap items-start justify-between gap-4 border-b border-border pb-5"><div><Link href="/sa/tenants" className="text-sm font-medium text-primary hover:underline">← Kembali ke tenant</Link><h1 className="mt-3 text-3xl font-bold tracking-tight text-ink">{detail.tenant.schoolName}</h1><p className="mt-1 text-sm text-ink-muted">{detail.tenant.npsn ?? detail.tenant.city ?? "Detail tenant"}</p></div><StatusBadge status={detail.tenant.isActive ? "green" : "red"} label={detail.tenant.isActive ? "Aktif" : "Nonaktif"} /></div><nav aria-label="Detail tenant" className="flex flex-wrap gap-x-5 gap-y-2 border-b border-border"><Link href={`/sa/tenants/${id}`} className={`min-h-[44px] pt-2 text-sm font-medium ${tab === "summary" ? "border-b-2 border-primary text-primary" : "text-ink-muted hover:text-ink"}`}>Ringkasan</Link>{tabs.slice(1).map(([key, label]) => <Link key={key} href={`/sa/tenants/${id}?tab=${key}`} className={`min-h-[44px] pt-2 text-sm font-medium ${tab === key ? "border-b-2 border-primary text-primary" : "text-ink-muted hover:text-ink"}`}>{label}</Link>)}</nav>{tab === "billing" ? <section><div className="mb-4"><h2 className="text-xl font-semibold text-ink">Billing tenant</h2><p className="mt-1 text-sm text-ink-muted">Status invoice dan konfirmasi bukti pembayaran.</p></div><TenantBillingTable initialInvoices={invoices} /></section> : tab === "users" ? <section><div className="mb-4"><h2 className="text-xl font-semibold text-ink">User tenant</h2><p className="mt-1 text-sm text-ink-muted">User aktif ditampilkan lebih dulu. Aksi sensitif tercatat di audit.</p></div><TenantUsersTable tenantId={id} initialUsers={users} /></section> : tab === "usage" ? <section><div className="mb-4"><h2 className="text-xl font-semibold text-ink">Usage tenant</h2><p className="mt-1 text-sm text-ink-muted">Jumlah operasional yang tersedia dari data penempatan dan akun.</p></div><div className="grid gap-4 border-y border-border py-4 sm:grid-cols-3 lg:grid-cols-6">{[["User aktif", usage.activeUsers], ["User nonaktif", usage.inactiveUsers], ["Siswa aktif", usage.activeStudents], ["Placement aktif", usage.activePlacements], ["Mentor aktif", usage.activeMentors], ["Guru aktif", usage.activeTeachers]].map(([label, value]) => <div key={label as string}><p className="text-2xl font-semibold text-ink">{value}</p><p className="text-sm text-ink-muted">{label}</p></div>)}</div>{plan && <p className="mt-4 text-sm text-ink-muted">Paket {plan.name}: maksimal {plan.maxStudents} siswa dan {plan.maxPlacements} placement.</p>}</section> : tab === "audit" ? <section><div className="mb-4"><h2 className="text-xl font-semibold text-ink">Audit tenant</h2><p className="mt-1 text-sm text-ink-muted">Aktivitas penting terbaru. Gunakan log platform untuk filter lengkap.</p></div><AuditList items={audit.items} /><Link href={`/sa/audit?tenantId=${id}`} className="mt-4 inline-block min-h-[44px] pt-2 text-sm font-medium text-primary hover:underline">Lihat log lengkap →</Link></section> : <TenantSummary detail={detail} plan={plan} usage={usage} />}</div>;
}
