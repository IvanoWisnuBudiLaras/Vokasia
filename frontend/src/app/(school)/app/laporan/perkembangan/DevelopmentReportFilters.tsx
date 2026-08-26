import type { CompanyDto, PeriodSummary } from "@/lib/apiTypes";

export interface DevelopmentReportFilterValues {
  periodId?: string;
  companyId?: string;
  stage?: string;
  status?: string;
  monitoringStatus?: string;
  search?: string;
  sort?: string;
  direction?: string;
  pageSize?: string;
}

export function DevelopmentReportFilters({
  periods,
  companies,
  values,
}: {
  periods: PeriodSummary[];
  companies: CompanyDto[];
  values: DevelopmentReportFilterValues;
}) {
  return (
    <form action="/app/laporan/perkembangan" method="get" className="flex flex-col gap-4 rounded-[var(--radius-lg)] border border-border bg-surface p-4 lg:p-5">
      <input type="hidden" name="page" value="1" />
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <div className="flex flex-col gap-1.5 xl:col-span-2"><label htmlFor="development-search" className="text-sm font-medium text-ink">Cari siswa atau DUDI</label><input id="development-search" name="search" type="search" defaultValue={values.search ?? ""} placeholder="Contoh: Siswa Beta" className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus" /></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-period" className="text-sm font-medium text-ink">Periode</label><select id="development-period" name="periodId" defaultValue={values.periodId ?? ""} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="">Semua periode</option>{periods.map((period) => <option key={period.id} value={period.id}>{period.name}</option>)}</select></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-company" className="text-sm font-medium text-ink">DUDI</label><select id="development-company" name="companyId" defaultValue={values.companyId ?? ""} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="">Semua DUDI</option>{companies.map((company) => <option key={company.id} value={company.id}>{company.name}</option>)}</select></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-stage" className="text-sm font-medium text-ink">Tahap penilaian</label><select id="development-stage" name="stage" defaultValue={values.stage ?? ""} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="">Semua tahap</option><option value="Middle">Middle</option><option value="Final">Final</option></select></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-status" className="text-sm font-medium text-ink">Status penilaian</label><select id="development-status" name="status" defaultValue={values.status ?? ""} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="">Semua status</option><option value="Draft">Draft</option><option value="Finalized">Selesai</option><option value="Reopened">Sedang diperbaiki</option></select></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-monitoring" className="text-sm font-medium text-ink">Monitoring Guru</label><select id="development-monitoring" name="monitoringStatus" defaultValue={values.monitoringStatus ?? ""} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="">Semua status</option><option value="ProgressingAsExpected">Sesuai rencana</option><option value="NeedsAttention">Perlu perhatian</option><option value="Problem">Masalah</option></select></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-sort" className="text-sm font-medium text-ink">Urutkan</label><select id="development-sort" name="sort" defaultValue={values.sort ?? "studentName"} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="studentName">Nama siswa</option><option value="companyName">DUDI</option><option value="periodName">Periode</option><option value="monitoringUpdatedAt">Monitoring terbaru</option></select></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-direction" className="text-sm font-medium text-ink">Arah urutan</label><select id="development-direction" name="direction" defaultValue={values.direction ?? "asc"} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="asc">A–Z / terlama</option><option value="desc">Z–A / terbaru</option></select></div>
        <div className="flex flex-col gap-1.5"><label htmlFor="development-page-size" className="text-sm font-medium text-ink">Baris per halaman</label><select id="development-page-size" name="pageSize" defaultValue={values.pageSize ?? "50"} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="25">25</option><option value="50">50</option><option value="100">100</option></select></div>
      </div>
      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border pt-4"><p className="text-xs text-ink-muted">Mengubah filter akan kembali ke halaman pertama.</p><button type="submit" className="inline-flex min-h-[var(--tap-min)] items-center justify-center rounded-[var(--radius-md)] bg-primary px-4 text-sm font-semibold text-on-primary outline-none hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-focus">Terapkan filter</button></div>
    </form>
  );
}
