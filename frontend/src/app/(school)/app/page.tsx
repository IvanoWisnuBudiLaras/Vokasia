import { ErrorState, EmptyState, Icon } from "@/components/ui";
import type { Paged, PeriodSummary, SchoolDashboardDto } from "@/lib/apiTypes";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import { DashboardBody } from "./DashboardBody";
import { PeriodSelector } from "./PeriodSelector";

// Data tenant-scoped per-user — jangan pernah di-cache statis (AGENTS.md #1, tenant isolation).
export const dynamic = "force-dynamic";

/**
 * VOK-H4-E2 §1 — Dashboard admin RAG (W3), menggantikan placeholder 2-kartu H2-E2/D19 (komentar
 * lama sendiri sudah menandai "KpiCards lengkap+ProblemStudentList tetap H4-E2" — ini pemenuhannya).
 * Server Component: fetch periods (utk selector) + GetSchoolDashboard(periodId terpilih dari URL
 * query, fallback periode pertama). Interaktivitas (pilih periode, buka drawer siswa) diserahkan
 * ke PeriodSelector.tsx/DashboardBody.tsx (client, terisolasi — pola sama D19/H3-E2).
 */
export default async function SchoolDashboardPage({
  searchParams,
}: {
  searchParams: Promise<{ periodId?: string }>;
}) {
  const session = await getSession();
  const params = await searchParams;

  let periods: PeriodSummary[] = [];
  let periodsError = false;
  try {
    const paged = await fetcher<Paged<PeriodSummary>>("/periods?pageSize=50");
    periods = paged.items;
  } catch (err) {
    console.error("[dashboard] gagal memuat daftar periode:", err);
    periodsError = true;
  }

  const selectedPeriodId = params.periodId ?? periods[0]?.id;

  let dashboard: SchoolDashboardDto | null = null;
  let dashboardError = false;
  if (selectedPeriodId) {
    try {
      dashboard = await fetcher<SchoolDashboardDto>(`/dashboard/school/${selectedPeriodId}`);
    } catch (err) {
      console.error("[dashboard] gagal memuat GetSchoolDashboard:", err);
      dashboardError = true;
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex flex-col gap-1">
          <h1 className="text-3xl font-extrabold tracking-tight text-ink">
            {session?.role === "Teacher" ? "Siswa Perlu Perhatian" : "Operasi PKL Sekolah"}
          </h1>
          <p className="text-base text-ink-muted">
            {session?.role === "Teacher"
              ? "Tangani siswa dengan prioritas tertinggi terlebih dahulu."
              : "Pantau ringkasan operasional dan progres penilaian harian."}
          </p>
        </div>
        {!periodsError && (
          <div className="shrink-0">
            <PeriodSelector periods={periods} value={selectedPeriodId ?? ""} />
          </div>
        )}
      </div>

      {periodsError && <ErrorState message="Daftar periode belum bisa dimuat." />}

      {!periodsError && periods.length === 0 && (
        <EmptyState
          icon={<Icon name="calendar-days" size={32} />}
          title="Belum ada periode"
          description="Buat periode dulu di menu Periode agar dashboard punya data untuk ditampilkan."
        />
      )}

      {!periodsError && periods.length > 0 && dashboardError && (
        <ErrorState message="Data dashboard belum bisa dimuat. Coba muat ulang halaman." />
      )}

      {dashboard && (
        <>
          {session?.role === "Teacher" ? (
            <section className="flex flex-col gap-4">
              <div className="border-l-4 border-status-red bg-surface-muted p-4 text-sm">
                <strong className="text-ink">Prioritas tertinggi ditampilkan lebih dulu.</strong>
                <span className="ml-1 text-ink-muted">Data berikut berasal dari status jurnal hari ini.</span>
              </div>
              <div id="siswa-bermasalah" className="flex flex-col gap-2">
                <h2 className="text-sm font-semibold text-ink">Siswa perlu perhatian</h2>
                {selectedPeriodId && <DashboardBody flagged={dashboard.flagged} periodId={selectedPeriodId} />}
              </div>
            </section>
          ) : (
            <section className="flex flex-col gap-5">
              <div className="border-y border-border py-4">
                <p className="text-sm font-semibold text-ink">Ringkasan periode</p>
                <p className="mt-1 text-sm text-ink-muted">Jurnal hari ini terisi {dashboard.journalTodayPct}%. {dashboard.pendingApprovals} jurnal menunggu persetujuan.</p>
              </div>
              <div id="siswa-bermasalah" className="flex flex-col gap-2">
                <div className="flex items-center justify-between gap-3">
                  <h2 className="text-sm font-semibold text-ink">Prioritas operasional</h2>
                  <a href="/app/operasi" className="text-sm font-semibold text-primary underline underline-offset-4">Lihat semua</a>
                </div>
                {selectedPeriodId && <DashboardBody flagged={dashboard.flagged.slice(0, 6)} periodId={selectedPeriodId} />}
              </div>
            </section>
          )}
        </>
      )}
    </div>
  );
}
