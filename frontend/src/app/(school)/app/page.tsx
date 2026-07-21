import { Card, ErrorState } from "@/components/ui";
import type { Paged, PeriodSummary } from "@/lib/apiTypes";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";

// Data tenant-scoped per-user — jangan pernah di-cache statis (AGENTS.md #1, tenant isolation).
export const dynamic = "force-dynamic";

interface DashboardStats {
  studentCount: number;
  activePlacementCount: number;
  periodName: string | null;
}

async function loadStats(): Promise<DashboardStats> {
  const students = await fetcher<Paged<unknown>>("/students?pageSize=1");
  const periods = await fetcher<Paged<PeriodSummary>>("/periods?pageSize=1");
  const currentPeriod = periods.items[0] ?? null;

  const activePlacementCount = currentPeriod
    ? (await fetcher<Paged<unknown>>(`/placements?periodId=${currentPeriod.id}&status=Active&pageSize=1`)).totalCount
    : 0;

  return {
    studentCount: students.totalCount,
    activePlacementCount,
    periodName: currentPeriod?.name ?? null,
  };
}

/**
 * VOK-H2-E2 — kartu ringkas dari endpoint nyata H2-E1 (students/periods/placements), bukti
 * wiring end-to-end M1 (bukan placeholder). KpiCards lengkap+ProblemStudentList tetap H4-E2.
 */
export default async function SchoolDashboardPage() {
  const session = await getSession();

  let stats: DashboardStats | null = null;
  let loadError = false;
  try {
    stats = await loadStats();
  } catch (err) {
    // GAP ditemukan+ditambal sesi VOK-H2-E3 (DECISIONS.md D17): catch INI dulu sepenuhnya diam
    // (tanpa log apa pun) — bikin kegagalan data-load sisi server MUSTAHIL didiagnosis dari log
    // (satu2nya jejak adalah pesan generik di UI). console.error di sini aman (server-side only,
    // tak pernah sampai ke client/browser) dan krusial utk operasional produksi nanti juga,
    // bukan cuma debugging sesi ini.
    console.error("[dashboard] loadStats gagal:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-ink">
        {session ? `Halo, ${session.name}` : "Dashboard Sekolah"}
      </h1>

      {loadError && (
        <ErrorState message="Data belum bisa dimuat — proxy BFF (VOK-H2-E3) belum tersedia." />
      )}

      {stats && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Card title="Jumlah Siswa">
            <p className="text-3xl font-semibold text-ink">{stats.studentCount}</p>
          </Card>
          <Card title={stats.periodName ? `Placement Aktif (${stats.periodName})` : "Placement Aktif"}>
            <p className="text-3xl font-semibold text-ink">{stats.activePlacementCount}</p>
          </Card>
        </div>
      )}
    </div>
  );
}
