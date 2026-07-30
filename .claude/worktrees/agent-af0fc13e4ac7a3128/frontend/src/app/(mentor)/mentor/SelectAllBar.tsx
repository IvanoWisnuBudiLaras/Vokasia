"use client";

import { Button } from "@/components/ui";

interface SelectAllBarProps {
  selectedCount: number;
  total: number;
  busy: boolean;
  onSelectAll: () => void;
  onClear: () => void;
  onApprove: () => void;
}

/** VOK-H3-E2 §2 SelectAllBar({selectedIds, total, onApprove}) — pilih semua -> 1 tap approve. */
export function SelectAllBar({ selectedCount, total, busy, onSelectAll, onClear, onApprove }: SelectAllBarProps) {
  const allSelected = total > 0 && selectedCount === total;

  return (
    <div className="flex items-center gap-2 rounded-[var(--radius-md)] bg-surface-muted px-3 py-2">
      <button
        type="button"
        onClick={allSelected ? onClear : onSelectAll}
        className="text-sm font-medium text-ink underline-offset-2 hover:underline"
      >
        {allSelected ? "Batalkan semua" : `Pilih semua (${total})`}
      </button>
      <span className="flex-1 text-right text-xs text-ink-muted">{selectedCount} dipilih</span>
      <Button size="md" onClick={onApprove} disabled={selectedCount === 0 || busy} loading={busy}>
        ✔ Approve ({selectedCount})
      </Button>
    </div>
  );
}
