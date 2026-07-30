"use client";

import { useMemo, useState } from "react";
import { Icon } from "@/components/ui";
import type { JournalDto } from "@/lib/apiTypes";

interface SamplePickerProps {
  approvedJournals: JournalDto[];
  selected: string[];
  max: number;
  onChange: (ids: string[]) => void;
}

/**
 * VOK-H6-E2 §3 SamplePicker — kurasi sampel portofolio dari jurnal APPROVED milik sendiri saja
 * (approvedJournals sudah difilter status=1 di server, page.tsx). Pola sama CompetencyPicker.tsx
 * (chip terpilih di atas, maks N, klik toggle) tp TANPA bottom-sheet modal (daftar jurnal disini
 * biasanya jauh lebih pendek drpd daftar kompetensi seluruh jurusan) — list datar cukup.
 */
export function SamplePicker({ approvedJournals, selected, max, onChange }: SamplePickerProps) {
  const [search, setSearch] = useState("");

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? approvedJournals.filter((j) => j.text.toLowerCase().includes(q)) : approvedJournals;
  }, [approvedJournals, search]);

  const atMax = selected.length >= max;

  function toggle(id: string) {
    if (selected.includes(id)) {
      onChange(selected.filter((s) => s !== id));
    } else if (!atMax) {
      onChange([...selected, id]);
    }
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-ink">Pilih sampel jurnal</span>
        <span className="text-xs text-ink-muted">
          {selected.length}/{max}
        </span>
      </div>

      {approvedJournals.length === 0 ? (
        <p className="rounded-[var(--radius-md)] border border-dashed border-border p-3 text-sm text-ink-muted">
          Belum ada jurnal yang disetujui untuk dijadikan sampel.
        </p>
      ) : (
        <>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Cari jurnal..."
            className="h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border px-3 text-sm outline-none focus:outline-2 focus:outline-primary focus:outline-offset-1"
          />
          <div className="flex max-h-72 flex-col gap-1.5 overflow-y-auto">
            {filtered.map((j) => {
              const isSelected = selected.includes(j.id);
              const disabled = !isSelected && atMax;
              return (
                <button
                  key={j.id}
                  type="button"
                  disabled={disabled}
                  onClick={() => toggle(j.id)}
                  className={`flex min-h-[var(--tap-min)] items-start gap-2 rounded-[var(--radius-md)] border p-2.5 text-left text-sm outline-none transition-[color,background-color,border-color] focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 ${
                    isSelected
                      ? "border-primary bg-primary-muted text-ink"
                      : "border-border text-ink hover:bg-surface-muted"
                  } ${disabled ? "cursor-not-allowed opacity-40 hover:bg-transparent active:translate-y-0" : "active:translate-y-px"}`}
                >
                  {isSelected ? (
                    <Icon name="check" size={16} className="mt-0.5 shrink-0 text-primary" />
                  ) : (
                    <span className="mt-0.5 h-4 w-4 shrink-0 rounded-full border border-border" aria-hidden="true" />
                  )}
                  <span className="flex-1">
                    <span className="block text-xs text-ink-muted">
                      {new Date(j.submittedAt).toLocaleDateString("id-ID")}
                    </span>
                    <span className="line-clamp-2">{j.text}</span>
                  </span>
                </button>
              );
            })}
            {filtered.length === 0 && <p className="py-4 text-center text-sm text-ink-muted">Tidak ada jurnal cocok.</p>}
          </div>
        </>
      )}
    </div>
  );
}
