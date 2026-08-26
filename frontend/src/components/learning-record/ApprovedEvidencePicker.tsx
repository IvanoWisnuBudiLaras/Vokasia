"use client";

import type { LearningAssessmentEvidenceCandidateDto } from "@/lib/apiTypes";

export function ApprovedEvidencePicker({
  criterionName,
  candidates,
  selectedIds,
  onToggle,
  disabled = false,
}: {
  criterionName: string;
  candidates: LearningAssessmentEvidenceCandidateDto[];
  selectedIds: string[];
  onToggle?: (journalEntryId: string) => void;
  disabled?: boolean;
}) {
  return (
    <section className="mt-3 border-t border-border/40 pt-3" aria-label={`Pilih bukti jurnal untuk ${criterionName}`}>
      <p className="text-sm font-medium text-ink">Pilih bukti jurnal untuk {criterionName}</p>
      <p className="mt-1 text-xs text-ink-muted">Hanya jurnal yang sudah disetujui dapat menjadi bukti.</p>
      {candidates.length === 0 ? <p className="mt-2 text-sm text-ink-muted">Belum ada jurnal Approved untuk placement ini.</p> : (
        <ul className="mt-2 space-y-2" role="list">
          {candidates.map((item) => (
            <li key={item.journalEntryId}>
              <label className="flex min-h-11 cursor-pointer items-start gap-3 rounded-[var(--radius-md)] border border-border/50 p-3 text-sm text-ink has-[:focus-visible]:outline has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-focus">
                <input type="checkbox" checked={selectedIds.includes(item.journalEntryId)} disabled={disabled} onChange={() => onToggle?.(item.journalEntryId)} />
                <span>{item.text}</span>
              </label>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
