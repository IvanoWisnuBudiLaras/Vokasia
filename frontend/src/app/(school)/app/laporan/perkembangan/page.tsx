import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import type { CompanyDto, LearningRecordReportResponseDto, Paged, PeriodSummary } from "@/lib/apiTypes";
import { DevelopmentReportFilters, type DevelopmentReportFilterValues } from "./DevelopmentReportFilters";
import { DevelopmentReportExportForm } from "./DevelopmentReportExportForm";
import { DevelopmentReportTable } from "./DevelopmentReportTable";

export const dynamic = "force-dynamic";

type SearchParams = Record<string, string | string[] | undefined>;

function valueOf(params: SearchParams, key: string): string | undefined {
  const value = params[key];
  return Array.isArray(value) ? value[0] : value;
}

function reportQuery(params: SearchParams): string {
  const query = new URLSearchParams();
  for (const key of ["periodId", "companyId", "stage", "status", "monitoringStatus", "search", "sort", "direction", "page", "pageSize"]) {
    const value = valueOf(params, key);
    if (value) query.set(key, value);
  }
  if (!query.has("page")) query.set("page", "1");
  if (!query.has("pageSize")) query.set("pageSize", "50");
  return query.toString();
}

export default async function DevelopmentReportPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const session = await getSession();
  const queryString = reportQuery(params);
  let periods: PeriodSummary[] = [];
  let companies: CompanyDto[] = [];
  let report: LearningRecordReportResponseDto | null = null;
  let loadError = false;

  try {
    const [periodPage, companyList, reportResponse] = await Promise.all([
      fetcher<Paged<PeriodSummary>>("/periods?pageSize=100"),
      fetcher<CompanyDto[]>("/companies"),
      fetcher<LearningRecordReportResponseDto>(`/teacher/learning-record/report?${queryString}`),
    ]);
    periods = periodPage.items;
    companies = companyList;
    report = reportResponse;
  } catch (error) {
    console.error("[laporan/perkembangan] gagal memuat laporan:", error);
    loadError = true;
  }

  const filters: DevelopmentReportFilterValues = {
    periodId: valueOf(params, "periodId"),
    companyId: valueOf(params, "companyId"),
    stage: valueOf(params, "stage"),
    status: valueOf(params, "status"),
    monitoringStatus: valueOf(params, "monitoringStatus"),
    search: valueOf(params, "search"),
    sort: valueOf(params, "sort"),
    direction: valueOf(params, "direction"),
    pageSize: valueOf(params, "pageSize"),
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1 border-b border-border pb-5"><p className="text-sm font-semibold uppercase tracking-[0.14em] text-primary">Laporan sekolah</p><h1 className="text-3xl font-extrabold tracking-tight text-ink">Laporan Perkembangan PKL</h1><p className="text-base text-ink-muted">Pantau kemajuan Learning Record dengan filter yang tetap aman di server.</p></header>

      {loadError && <ErrorState message="Laporan perkembangan belum bisa dimuat. Coba muat ulang halaman." />}
      {!loadError && report && (
        <>
          <section aria-labelledby="development-report-summary" className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
            <h2 id="development-report-summary" className="sr-only">Ringkasan laporan perkembangan</h2>
            <SummaryCard label="Total penempatan" value={report.summary.totalCount} />
            <SummaryCard label="Selesai" value={report.summary.completeCount} />
            <SummaryCard label="Belum selesai" value={report.summary.incompleteCount} />
            <SummaryCard label="Perlu perhatian" value={report.summary.needsAttentionCount} accent={report.summary.needsAttentionCount > 0} />
          </section>
          <DevelopmentReportFilters periods={periods} companies={companies} values={filters} />
          <DevelopmentReportExportForm report={report} queryString={queryString} />
          {report.totalCount === 0 && periods.length === 0 ? <EmptyState icon={<Icon name="file-text" size={32} />} title="Belum ada data laporan" description="Tambahkan periode dan placement untuk mulai melihat perkembangan." /> : <DevelopmentReportTable report={report} queryString={queryString} canReopen={session?.role === "TenantAdmin"} />}
        </>
      )}
    </div>
  );
}

function SummaryCard({ label, value, accent = false }: { label: string; value: number; accent?: boolean }) {
  return <div className={`rounded-[var(--radius-lg)] border p-4 ${accent ? "border-status-amber/40 bg-status-amber/5" : "border-border bg-surface"}`}><p className="text-sm text-ink-muted">{label}</p><p className="mt-2 text-2xl font-bold tracking-tight text-ink">{value}</p></div>;
}
