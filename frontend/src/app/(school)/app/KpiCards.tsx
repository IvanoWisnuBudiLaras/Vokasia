import { Card } from "@/components/ui";

export interface KpiCardsProps {
  journalTodayPct: number;
  pendingApprovals: number;
  lateVisits: number;
  flaggedCount: number;
}

/**
 * VOK-H4-E2 §1 KpiCards — 4 kartu W3. Hierarki hallmark-flow (DECISIONS.md D19, dilanjutkan
 * di sini): "Siswa Bermasalah" jadi LEAD (kartu terbesar, aksen merah) — bukan equal-card default —
 * krn early-warning (Gate M3) adalah TUJUAN UTAMA layar ini, angka yang paling perlu menarik mata
 * admin duluan. 3 kartu lain (jurnal hari ini/approval pending/kunjungan terlambat) jadi pendukung,
 * ukuran seragam SATU LEVEL di bawah lead (bukan berarti "equal-card" — mereka equal SATU SAMA
 * LAIN scr sengaja krn ketiganya operasional setara, beda dari lead yang memang beda kelas).
 * Pola angka+kata Stat-Led + tabular-nums dipertahankan persis dari D19, tanpa animasi count-up
 * (motion-cut, DESIGN.md beku).
 */
export function KpiCards({ journalTodayPct, pendingApprovals, lateVisits, flaggedCount }: KpiCardsProps) {
  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
      <Card className={flaggedCount > 0 ? "border-status-red/30 bg-status-red-bg md:col-span-4 lg:col-span-1" : "md:col-span-4 lg:col-span-1"}>
        <div className="flex flex-col gap-1">
          <p className="text-xs font-medium uppercase tracking-wide text-ink-muted">Siswa Bermasalah</p>
          <p className={"text-5xl font-semibold tabular-nums " + (flaggedCount > 0 ? "text-status-red" : "text-ink")}>
            {flaggedCount}
          </p>
          <p className="text-sm text-ink-muted">
            {flaggedCount > 0 ? "Butuh tindak lanjut segera — lihat daftar di bawah." : "Tidak ada siswa bermasalah hari ini."}
          </p>
        </div>
      </Card>

      <Card>
        <div className="flex flex-col gap-1">
          <p className="text-xs font-medium uppercase tracking-wide text-ink-muted">Jurnal Hari Ini</p>
          <p className="text-3xl font-semibold tabular-nums text-ink">{journalTodayPct.toFixed(1)}%</p>
          <p className="text-sm text-ink-muted">Slot terisi dari total slot aktif.</p>
        </div>
      </Card>

      <Card>
        <div className="flex flex-col gap-1">
          <p className="text-xs font-medium uppercase tracking-wide text-ink-muted">Approval Pending</p>
          <p className="text-3xl font-semibold tabular-nums text-ink">{pendingApprovals}</p>
          <p className="text-sm text-ink-muted">Jurnal menunggu approval mentor.</p>
        </div>
      </Card>

      <Card>
        <div className="flex flex-col gap-1">
          <p className="text-xs font-medium uppercase tracking-wide text-ink-muted">Kunjungan Terlambat</p>
          <p className="text-3xl font-semibold tabular-nums text-ink">{lateVisits}</p>
          <p className="text-sm text-ink-muted">Belum ada jadwal kunjungan tercatat.</p>
        </div>
      </Card>
    </div>
  );
}
