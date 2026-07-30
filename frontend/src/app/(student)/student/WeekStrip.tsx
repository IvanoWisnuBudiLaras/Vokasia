import type { WeekDayStatusDto } from "@/lib/apiTypes";
import { JournalSlotStatus } from "@/lib/apiTypes";
import { Icon } from "@/components/ui";

const DAY_LABEL = ["Sen", "Sel", "Rab", "Kam", "Jum"];

/**
 * VOK-H3-E2 §1 WeekStrip. Wireframe W1 (PRD 4.3) menunjukkan 3 simbol (✅ selesai / 🟡 perlu
 * perhatian / ⬜ kosong) — [ASSUMPTION dicatat, bukan diam-diam]: TodayJournalDto.weekStatus
 * (backend H3-E1) hanya membawa JournalSlotStatus per hari (Empty/Filled), BUKAN status approval
 * entry (Submitted/Approved/Rejected) per hari — jadi tak ada data utk beda-kan "terisi tapi
 * masih pending" (🟡) dari "terisi" scr umum. Menambah field itu butuh ubah backend
 * (GetTodayJournal, Vokasia.Api) — DI LUAR wilayah ticket ini (`frontend/` saja). Disederhanakan
 * jujur jadi 2 simbol (✅ terisi / ⬜ kosong) sesuai data yang BENAR tersedia, bukan mengarang status
 * ketiga. Dicatat sbg gap utk H3+ berikutnya kalau backend ingin diperkaya.
 */
export function WeekStrip({ days, streak }: { days: WeekDayStatusDto[]; streak: number }) {
  return (
    <div className="flex items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface-muted px-3 py-2.5">
      <ul className="flex min-w-0 items-center gap-1.5" aria-label="Status jurnal minggu ini, Senin sampai Jumat">
        {days.map((d, i) => (
          <li
            key={d.date}
            title={`${DAY_LABEL[i] ?? ""} — ${d.status === JournalSlotStatus.Filled ? "terisi" : "kosong"}`}
            className="flex min-w-8 flex-col items-center gap-1 text-xs text-ink-muted"
          >
            <span>{DAY_LABEL[i] ?? ""}</span>
            {d.status === JournalSlotStatus.Filled ? (
              <span className="flex h-5 w-5 items-center justify-center rounded-full bg-status-green text-primary-ink">
                <Icon name="check" size={16} />
                <span className="sr-only">Terisi</span>
              </span>
            ) : (
              <span className="h-5 w-5 rounded-full border border-border bg-surface">
                <span className="sr-only">Kosong</span>
              </span>
            )}
          </li>
        ))}
      </ul>
      <span className="shrink-0 text-sm font-medium text-ink">
        Streak <span className="tabular-nums">{streak}</span>
      </span>
    </div>
  );
}
