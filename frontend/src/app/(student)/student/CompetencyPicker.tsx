"use client";

import { useMemo, useState } from "react";
import { Button } from "@/components/ui";
import { cn } from "@/lib/cn";
import type { CompetencyDto } from "@/lib/apiTypes";

interface CompetencyPickerProps {
  options: CompetencyDto[];
  selected: string[];
  max: number;
  onChange: (ids: string[]) => void;
}

/** VOK-H3-E2 §1 CompetencyPicker — bottom-sheet mobile, cari cepat, chip terpilih di atas (maks 5). */
export function CompetencyPicker({ options, selected, max, onChange }: CompetencyPickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? options.filter((o) => o.name.toLowerCase().includes(q)) : options;
  }, [options, search]);

  const selectedOptions = options.filter((o) => selected.includes(o.id));
  const atMax = selected.length >= max;

  function toggle(id: string) {
    if (selected.includes(id)) {
      onChange(selected.filter((s) => s !== id));
    } else if (!atMax) {
      onChange([...selected, id]);
    }
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-ink">Kompetensi</span>
        <span className="text-xs text-ink-muted">
          {selected.length}/{max}
        </span>
      </div>

      <div className="flex flex-wrap gap-2">
        {selectedOptions.map((o) => (
          <button
            key={o.id}
            type="button"
            onClick={() => toggle(o.id)}
            className="inline-flex h-9 items-center gap-1.5 rounded-full bg-primary px-3 text-sm text-primary-ink"
          >
            {o.name}
            <span aria-hidden="true">✕</span>
          </button>
        ))}
        <button
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex h-9 items-center gap-1 rounded-full border border-dashed border-border px-3 text-sm text-ink-muted hover:bg-surface-muted"
        >
          + pilih
        </button>
      </div>

      {open && (
        <div
          className="fixed inset-0 z-50 flex items-end justify-center bg-ink/40 sm:items-center"
          onClick={() => setOpen(false)}
        >
          <div
            className="flex max-h-[70vh] w-full flex-col rounded-t-[var(--radius-lg)] border border-border bg-surface p-4 sm:max-w-md sm:rounded-[var(--radius-lg)]"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-base font-semibold text-ink">Pilih kompetensi (maks {max})</h2>
              <button type="button" onClick={() => setOpen(false)} aria-label="Tutup" className="p-1 text-ink-muted">
                ✕
              </button>
            </div>

            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Cari kompetensi..."
              className="mb-3 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border px-3 text-base outline-none focus:outline-2 focus:outline-primary focus:outline-offset-1"
            />

            <div className="flex-1 overflow-y-auto">
              {filtered.length === 0 && (
                <p className="py-6 text-center text-sm text-ink-muted">Tidak ada kompetensi cocok.</p>
              )}
              <div className="flex flex-col gap-1">
                {filtered.map((o) => {
                  const isSelected = selected.includes(o.id);
                  const disabled = !isSelected && atMax;
                  return (
                    <button
                      key={o.id}
                      type="button"
                      disabled={disabled}
                      onClick={() => toggle(o.id)}
                      className={cn(
                        "flex min-h-[var(--tap-min)] items-center justify-between rounded-[var(--radius-md)] px-3 text-left text-sm",
                        isSelected ? "bg-primary-muted text-ink" : "text-ink hover:bg-surface-muted",
                        disabled && "opacity-40"
                      )}
                    >
                      {o.name}
                      {isSelected && <span aria-hidden="true">✓</span>}
                    </button>
                  );
                })}
              </div>
            </div>

            <Button type="button" size="lg" className="mt-3 w-full" onClick={() => setOpen(false)}>
              Selesai
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
