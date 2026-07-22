import { ErrorState, EmptyState } from "@/components/ui";
import type { Paged, PeriodSummary, SchoolDashboardDto } from "@/lib/apiTypes";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import { DashboardBody } from "./DashboardBody";
import { KpiCards } from "./KpiCards";
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
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-xl font-semibold text-ink">
          {session ? `Halo, ${session.name}` : "Dashboard Sekolah"}
        </h1>
        {!periodsError && <PeriodSelector periods={periods} value={selectedPeriodId ?? ""} />}
      </div>

      {periodsError && <ErrorState message="Daftar periode belum bisa dimuat." />}

      {!periodsError && periods.length === 0 && (
        <EmptyState
          icon="🗓️"
          title="Belum ada periode"
          description="Buat periode dulu di menu Periode agar dashboard punya data untuk ditampilkan."
        />
      )}

      {!periodsError && periods.length > 0 && dashboardError && (
        <ErrorState message="Data dashboard belum bisa dimuat. Coba muat ulang halaman." />
      )}

      {dashboard && (
        <>
          <KpiCards
            journalTodayPct={dashboard.journalTodayPct}
            pendingApprovals={dashboard.pendingApprovals}
            lateVisits={dashboard.lateVisits}
            flaggedCount={dashboard.flagged.length}
          />

          <div className="flex flex-col gap-2">
            <h2 className="text-sm font-semibold text-ink">Siswa Bermasalah</h2>
            {selectedPeriodId && <DashboardBody flagged={dashboard.flagged} periodId={selectedPeriodId} />}
          </div>
        </>
      )}
    </div>
  );
}
