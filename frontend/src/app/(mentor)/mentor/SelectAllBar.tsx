"use client";

import { Button, Icon } from "@/components/ui";

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
    <div className="flex flex-wrap items-center gap-2 rounded-[var(--radius-md)] bg-surface-muted px-3 py-2">
      <button
        type="button"
        onClick={allSelected ? onClear : onSelectAll}
        className="min-h-[var(--tap-min)] text-sm font-medium text-ink underline-offset-2 outline-none hover:underline focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
      >
        {allSelected ? "Batalkan semua" : `Pilih semua (${total})`}
      </button>
      <span className="flex-1 text-right text-xs text-ink-muted">{selectedCount} dipilih</span>
      <Button size="lg" onClick={onApprove} disabled={selectedCount === 0 || busy} loading={busy}>
        <Icon name="check" size={16} /> Setujui ({selectedCount})
      </Button>
    </div>
  );
}
