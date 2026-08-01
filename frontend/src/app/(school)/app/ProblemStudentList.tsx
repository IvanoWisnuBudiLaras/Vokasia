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
    return (
      <EmptyState
        icon={<Icon name="check" size={32} />}
        title="Tidak ada siswa bermasalah"
        description="Semua siswa berstatus hijau hari ini — jurnal terisi sesuai jadwal."
      />
    );
  }

  const sorted = [...items].sort((a, b) => b.rag - a.rag);

  const handleResolve = (e: React.MouseEvent, id: string, name: string) => {
    e.stopPropagation();
    setResolvedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
    setActionAlert(`Status intervensi untuk ${name} berhasil diperbarui.`);
    setTimeout(() => setActionAlert(null), 3000);
  };

  const handleSendWarning = (e: React.MouseEvent, name: string) => {
    e.stopPropagation();
    setActionAlert(`📢 Peringatan resmi via Email & WA telah dikirimkan ke siswa ${name} dan Guru Pembimbing.`);
    setTimeout(() => setActionAlert(null), 4000);
  };

  return (
    <div className="flex flex-col gap-3">
      {actionAlert && (
        <div className="rounded-[var(--radius-md)] border border-primary/30 bg-primary/10 p-3 text-xs font-semibold text-primary animate-fade-in flex items-center justify-between">
          <span>{actionAlert}</span>
          <button onClick={() => setActionAlert(null)} className="text-primary hover:opacity-70 font-bold">✕</button>
        </div>
      )}

      <ul className="flex flex-col gap-2">
        {sorted.map((student) => {
          const isResolved = resolvedIds.has(student.studentId);
          return (
            <li key={student.studentId}>
              <div
                onClick={() => onSelect(student)}
                className={
                  "flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3.5 text-left cursor-pointer outline-none " +
                  "transition-[color,background-color,border-color] hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus " +
                  (isResolved ? "opacity-75 bg-surface-muted/60" : "")
                }
              >
                <div className="flex flex-col gap-0.5">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-semibold text-ink">{student.name}</span>
                    {isResolved && (
                      <span className="rounded-full bg-status-green-bg px-2 py-0.5 text-[10px] font-bold text-status-green border border-status-green/30">
                        ✓ Sedang Diproses
                      </span>
                    )}
                  </div>
                  <span className="text-xs text-ink-muted">{student.companyName}</span>
                </div>

                <div className="flex items-center gap-3">
                  <span className="hidden text-xs text-ink-muted md:inline">{student.reason}</span>
                  <StatusBadge
                    status={isResolved ? "green" : ragToBadgeStatus(student.rag)}
                    label={isResolved ? "Diproses" : student.rag === RagStatus.Red ? "Merah" : "Kuning"}
                  />

                  {/* Intervention Workflow Action Buttons */}
                  <div className="flex items-center gap-1.5" onClick={(e) => e.stopPropagation()}>
                    <button
                      type="button"
                      title="Kirim peringatan email & WA"
                      onClick={(e) => handleSendWarning(e, student.name)}
                      className="inline-flex h-8 items-center gap-1 rounded-[var(--radius-md)] border border-status-amber/40 bg-status-amber-bg px-2.5 text-xs font-semibold text-status-amber hover:bg-status-amber/20 transition-colors"
                    >
                      ⚠️ Peringatkan
                    </button>

                    <button
                      type="button"
                      title={isResolved ? "Tandai belum ditindaklanjuti" : "Tandai sedang diproses"}
                      onClick={(e) => handleResolve(e, student.studentId, student.name)}
                      className="inline-flex h-8 items-center gap-1 rounded-[var(--radius-md)] border border-primary/40 bg-primary/10 px-2.5 text-xs font-semibold text-primary hover:bg-primary/20 transition-colors"
                    >
                      {isResolved ? "↩ Buka Lagi" : "✓ Tandai Diproses"}
                    </button>
                  </div>
                </div>
              </div>
            </li>
          );
        })}
      </ul>
    </div>
  );
}
