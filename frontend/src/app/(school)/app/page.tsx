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
        // Hallmark-flow, component-scope (DECISIONS.md D19): sebelumnya 2 kartu SAMA besar
        // berdampingan — "equal-card default" yang hallmark macrostructures.md tandai sbg tanda
        // AI-generik ("visual rhythm harus dari variasi ukuran, bukan keseragaman kartu"). Diganti
        // hierarki asli: Placement Aktif jadi lead (2 kolom, angka lebih besar) krn itu metrik
        // operasional yang berubah tiap hari (yang dicek admin sekolah paling sering); Jumlah
        // Siswa jadi pendukung (1 kolom). Pola angka+kata dari macrostructure Stat-Led hallmark
        // ("angka besar TIDAK PERNAH berdiri sendiri sbg headline, wajib disandingkan baris kata")
        // diterapkan di skala kartu, bukan skala hero halaman — tanpa animasi count-up (motion.md
        // hallmark sarankan itu utk hero marketing; DESIGN.md membekukan stance motion-cut utk
        // app fungsional ini, jadi angka tampil langsung). Kedua angka memakai tabular-nums (rata
        // kanan visual saat berubah, konvensi Stat-Led). Tak ada metrik baru/karangan — tetap 2
        // angka nyata yang sama dari loadStats().
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <Card className="md:col-span-2">
            <div className="flex flex-col gap-1">
              <p className="text-xs font-medium uppercase tracking-wide text-ink-muted">
                Placement Aktif{stats.periodName ? ` · ${stats.periodName}` : ""}
              </p>
              <p className="text-5xl font-semibold tabular-nums text-ink">{stats.activePlacementCount}</p>
              <p className="text-sm text-ink-muted">
                {stats.periodName
                  ? `Siswa PKL berjalan periode ${stats.periodName}.`
                  : "Belum ada periode aktif — buat periode dulu di menu Periode."}
              </p>
            </div>
          </Card>
          <Card className="md:col-span-1">
            <div className="flex flex-col gap-1">
              <p className="text-xs font-medium uppercase tracking-wide text-ink-muted">Jumlah Siswa</p>
              <p className="text-3xl font-semibold tabular-nums text-ink">{stats.studentCount}</p>
              <p className="text-sm text-ink-muted">Total siswa terdaftar.</p>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
