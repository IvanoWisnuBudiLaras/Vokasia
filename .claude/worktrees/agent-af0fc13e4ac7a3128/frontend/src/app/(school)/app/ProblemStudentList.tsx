import { EmptyState, StatusBadge } from "@/components/ui";
import { ragToBadgeStatus, RagStatus, type DashboardFlaggedStudentDto } from "@/lib/apiTypes";

export interface ProblemStudentListProps {
  items: DashboardFlaggedStudentDto[];
  onSelect: (student: DashboardFlaggedStudentDto) => void;
}

/**
 * VOK-H4-E2 §1 ProblemStudentList — daftar 🔴🟡 terurut severity (Red dulu) + alasan + link
 * detail siswa (StudentDetailDrawer, dibuka via onSelect — state drawer dipegang parent client
 * component DashboardBody, list ini sendiri cukup presentational + 1 callback, bukan pemilik state).
 */
export function ProblemStudentList({ items, onSelect }: ProblemStudentListProps) {
  if (items.length === 0) {
    return (
      <EmptyState
        icon="✅"
        title="Tidak ada siswa bermasalah"
        description="Semua siswa berstatus hijau hari ini — jurnal terisi sesuai jadwal."
      />
    );
  }

  const sorted = [...items].sort((a, b) => {
    // Red (2) dulu, lalu Amber (1) — severity menurun, bukan urutan API apa adanya.
    return b.rag - a.rag;
  });

  return (
    <ul className="flex flex-col gap-2">
      {sorted.map((student) => (
        <li key={student.studentId}>
          <button
            type="button"
            onClick={() => onSelect(student)}
            className={
              "flex w-full items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3 text-left outline-none " +
              "transition-colors hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
            }
          >
            <div className="flex flex-col gap-0.5">
              <span className="text-sm font-medium text-ink">{student.name}</span>
              <span className="text-xs text-ink-muted">{student.companyName}</span>
            </div>
            <div className="flex items-center gap-3">
              <span className="hidden text-xs text-ink-muted sm:inline">{student.reason}</span>
              <StatusBadge
                status={ragToBadgeStatus(student.rag)}
                label={student.rag === RagStatus.Red ? "Merah" : "Kuning"}
              />
            </div>
          </button>
        </li>
      ))}
    </ul>
  );
}
