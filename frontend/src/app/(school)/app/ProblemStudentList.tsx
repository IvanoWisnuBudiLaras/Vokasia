"use client";

import { useState } from "react";
import { EmptyState, Icon, StatusBadge } from "@/components/ui";
import { ragToBadgeStatus, RagStatus, type DashboardFlaggedStudentDto } from "@/lib/apiTypes";

export interface ProblemStudentListProps {
  items: DashboardFlaggedStudentDto[];
  onSelect: (student: DashboardFlaggedStudentDto) => void;
}

export function ProblemStudentList({ items, onSelect }: ProblemStudentListProps) {
  const [resolvedIds, setResolvedIds] = useState<Set<string>>(new Set());
  const [actionAlert, setActionAlert] = useState<string | null>(null);

  if (items.length === 0) {
    return <EmptyState icon={<Icon name="check" size={32} />} title="Tidak ada siswa bermasalah" description="Semua siswa berstatus hijau hari ini — jurnal terisi sesuai jadwal." />;
  }

  const sorted = [...items].sort((a, b) => b.rag - a.rag);
  function toggleResolved(event: React.MouseEvent, student: DashboardFlaggedStudentDto) {
    event.stopPropagation();
    setResolvedIds((previous) => {
      const next = new Set(previous);
      if (next.has(student.studentId)) next.delete(student.studentId); else next.add(student.studentId);
      return next;
    });
    setActionAlert(`Tindak lanjut ${student.name} diperbarui.`);
    setTimeout(() => setActionAlert(null), 3000);
  }

  return (
    <div className="flex flex-col gap-3">
      {actionAlert && <p role="status" className="border border-primary/30 bg-primary/10 p-3 text-sm font-medium text-primary">{actionAlert}</p>}
      <ul className="flex flex-col gap-2">
        {sorted.map((student) => {
          const resolved = resolvedIds.has(student.studentId);
          return (
            <li key={student.studentId}>
              <div className={`flex flex-wrap items-center justify-between gap-3 border border-border bg-surface p-3 ${resolved ? "opacity-75" : ""}`}>
                <button type="button" onClick={() => onSelect(student)} className="min-h-11 min-w-0 flex-1 text-left outline-none focus-visible:outline-2 focus-visible:outline-focus">
                  <span className="block text-sm font-semibold text-ink">{student.name}</span>
                  <span className="block text-xs text-ink-muted">{student.companyName}</span>
                  <span className="mt-1 block text-xs text-ink-muted">{student.reason}</span>
                </button>
                <div className="flex items-center gap-2">
                  <StatusBadge status={resolved ? "green" : ragToBadgeStatus(student.rag)} label={resolved ? "Diproses" : student.rag === RagStatus.Red ? "Merah" : "Kuning"} />
                  <button type="button" onClick={(event) => toggleResolved(event, student)} className="min-h-11 border border-primary px-3 text-xs font-semibold text-primary focus-visible:outline-2 focus-visible:outline-focus">
                    {resolved ? "Buka lagi" : "Tandai diproses"}
                  </button>
                </div>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
