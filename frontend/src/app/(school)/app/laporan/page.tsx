import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import { JournalEntryStatus, type DashboardFlaggedStudentDto, type JournalReportRowDto, type Paged, type PeriodSummary, type RecapRowDto, type SchoolDashboardDto } from "@/lib/apiTypes";
import { AssessmentReportTable, JournalReportTable, PriorityReportTable } from "./ReportingTables";

export const dynamic = "force-dynamic";

type ReportKey = "home" | "journals" | "assessments" | "priorities";

function reportKey(value: string | undefined): ReportKey {
  if (value === "journals" || value === "assessments" || value === "priorities") return value;
  return "home";
}

function Finding({ title, context, href, action }: { title: string; context: string; href: string; action: string }) {
  return (
    <li className="flex flex-col gap-2 py-4 sm:flex-row sm:items-center sm:justify-between">
      <div><h3 className="font-medium text-ink">{title}</h3><p className="text-sm text-ink-muted">{context}</p></div>
      <a href={href} className="inline-flex min-h-[var(--tap-min)] shrink-0 items-center gap-1 text-sm font-semibold text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-focus">{action} <Icon name="arrow-right" size={16} /></a>
    </li>
  );
}

function PeriodFilter({ periods, value, report }: { periods: PeriodSummary[]; value: string; report: ReportKey }) {
  return (
    <form action="/app/laporan" method="get" className="flex flex-wrap items-center gap-2">
      {report !== "home" && <input type="hidden" name="report" value={report} />}
      <label htmlFor="report-period" className="text-sm text-ink-muted">Periode</label>
      <select id="report-period" name="periodId" defaultValue={value} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2">
        {periods.map((period) => <option key={period.id} value={period.id}>{period.name}</option>)}
      </select>
      <button type="submit" className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border px-3 text-sm font-medium text-primary outline-none hover:bg-primary-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2">Terapkan</button>
    </form>
  );
}

function ReportNavigation({ periodId }: { periodId: string }) {
  return <nav aria-label="Jenis laporan" className="flex flex-wrap gap-x-4 gap-y-2 border-b border-border pb-3 text-sm"><a href={`/app/laporan?periodId=${periodId}`} className="font-semibold text-primary underline underline-offset-4">Ringkasan</a><a href={`/app/laporan?periodId=${periodId}&report=assessments&status=incomplete`} className="text-ink-muted underline underline-offset-4">Penilaian belum selesai</a><a href={`/app/laporan?periodId=${periodId}&report=journals&status=waiting`} className="text-ink-muted underline underline-offset-4">Jurnal menunggu review</a><a href={`/app/laporan?periodId=${periodId}&report=priorities`} className="text-ink-muted underline underline-offset-4">Siswa perlu perhatian</a></nav>;
}

export default async function ReportingPage({ searchParams }: { searchParams: Promise<{ periodId?: string; report?: string; status?: string }> }) {
  const params = await searchParams;
  const report = reportKey(params.report);
  const session = await getSession();
  let periods: PeriodSummary[] = [];
  let selectedPeriod: PeriodSummary | undefined;
  let dashboard: SchoolDashboardDto | null = null;
  let recapRows: RecapRowDto[] = [];
  let journalRows: JournalReportRowDto[] = [];
  let loadError = false;

  try {
    periods = (await fetcher<Paged<PeriodSummary>>("/periods?pageSize=50")).items;
    selectedPeriod = periods.find((period) => period.id === params.periodId) ?? periods[0];
    if (selectedPeriod) {
      const periodId = selectedPeriod.id;
      if (report === "home" || report === "priorities") dashboard = await fetcher<SchoolDashboardDto>(`/dashboard/school/${periodId}`);
      if (report === "home" || report === "assessments") recapRows = await fetcher<RecapRowDto[]>(`/periods/${periodId}/grade-recap`);
      if (report === "journals") journalRows = await fetcher<JournalReportRowDto[]>(`/reports/school/${periodId}/journals?status=${JournalEntryStatus.Submitted}`);
    }
  } catch (err) {
    console.error("[laporan] gagal memuat laporan:", err);
    loadError = true;
  }

  const incompleteAssessmentRows = recapRows.filter((row) => row.status !== "Final");
  const flaggedRows: DashboardFlaggedStudentDto[] = dashboard?.flagged ?? [];

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-4 border-b border-border pb-5 lg:flex-row lg:items-end lg:justify-between">
        <div><h1 className="text-3xl font-extrabold tracking-tight text-ink">Laporan</h1><p className="mt-1 text-base text-ink-muted">Temukan hal yang perlu ditindaklanjuti dari data periode ini.</p></div>
        {!loadError && periods.length > 0 && selectedPeriod && <PeriodFilter periods={periods} value={selectedPeriod.id} report={report} />}
      </div>

      {loadError && <ErrorState message="Laporan belum bisa dimuat. Coba muat ulang halaman." />}
      {!loadError && periods.length === 0 && <EmptyState icon={<Icon name="file-text" size={32} />} title="Belum ada periode" description="Buat periode PKL terlebih dahulu agar laporan dapat ditampilkan." />}

      {!loadError && selectedPeriod && (
        <>
          {report !== "home" && <ReportNavigation periodId={selectedPeriod.id} />}
          {report === "home" && dashboard && (
            <section aria-labelledby="findings-heading" className="flex flex-col gap-3">
              <div><h2 id="findings-heading" className="text-lg font-semibold text-ink">Perlu perhatian</h2><p className="text-sm text-ink-muted">Temuan periode {selectedPeriod.name} dengan data pendukung dan jalur tindak lanjut.</p></div>
              <ul className="divide-y divide-border border-y border-border">
                {dashboard.pendingApprovals > 0 && <Finding title={`${dashboard.pendingApprovals} jurnal menunggu review`} context="Jurnal siswa yang sudah dikirim dan belum disetujui mentor." href={`/app/laporan?periodId=${selectedPeriod.id}&report=journals&status=waiting`} action="Lihat jurnal" />}
                {incompleteAssessmentRows.length > 0 && <Finding title={`${incompleteAssessmentRows.length} penilaian belum selesai`} context="Data rekap menunjukkan nilai yang belum lengkap atau belum dinilai." href={`/app/laporan?periodId=${selectedPeriod.id}&report=assessments&status=incomplete`} action="Lihat penilaian" />}
                {flaggedRows.length > 0 && <Finding title={`${flaggedRows.length} siswa perlu perhatian`} context="Status jurnal hari ini menandai siswa yang perlu ditindaklanjuti." href={`/app/laporan?periodId=${selectedPeriod.id}&report=priorities`} action="Lihat siswa" />}
                {dashboard.pendingApprovals === 0 && incompleteAssessmentRows.length === 0 && flaggedRows.length === 0 && <li role="status" className="py-6 text-sm text-ink-muted">Tidak ada temuan yang perlu ditindaklanjuti pada periode ini.</li>}
              </ul>
            </section>
          )}
          {report === "assessments" && <AssessmentReportTable periodId={selectedPeriod.id} rows={params.status === "incomplete" ? incompleteAssessmentRows : recapRows} filtered={params.status === "incomplete"} canExport={session?.role === "TenantAdmin" || session?.role === "DeptHead"} />}
          {report === "journals" && <JournalReportTable rows={journalRows} />}
          {report === "priorities" && <PriorityReportTable rows={flaggedRows} periodId={selectedPeriod.id} />}
        </>
      )}
    </div>
  );
}
