"use client";

import { EmptyState, Icon, StatusBadge } from "@/components/ui";
import { ragLabel, ragToBadgeStatus, type DashboardFlaggedStudentDto } from "@/lib/apiTypes";

export interface ProblemStudentListProps {
  items: DashboardFlaggedStudentDto[];
  onSelect: (student: DashboardFlaggedStudentDto) => void;
}

export function ProblemStudentList({ items, onSelect }: ProblemStudentListProps) {
  if (items.length === 0) {
    return <EmptyState icon={<Icon name="check" size={32} />} title="Tidak ada siswa bermasalah" description="Semua siswa berstatus normal hari ini — jurnal terisi sesuai jadwal." />;
  }

  const sorted = [...items].sort((a, b) => b.rag - a.rag);
  return (
    <div>
      <ul className="divide-y divide-border border-y border-border">
        {sorted.map((student) => {
          return (
            <li key={student.studentId} className="flex flex-wrap items-center justify-between gap-3 py-4">
              <button type="button" onClick={() => onSelect(student)} className="min-h-11 min-w-0 flex-1 text-left outline-none focus-visible:outline-2 focus-visible:outline-focus">
                <span className="flex flex-wrap items-center gap-2 text-sm font-semibold text-ink">
                  {student.name}
                  <StatusBadge status={ragToBadgeStatus(student.rag)} label={ragLabel(student.rag)} />
                </span>
                <span className="mt-1 block text-xs text-ink-muted">{student.companyName}</span>
                <span className="mt-1 block text-sm text-ink">{student.reason}</span>
              </button>
              <button type="button" onClick={() => onSelect(student)} className="min-h-11 shrink-0 border border-primary px-3 text-xs font-semibold text-primary focus-visible:outline-2 focus-visible:outline-focus">
                Tinjau jurnal
              </button>
              </li>
          );
        })}
      </ul>
    </div>
  );
}
