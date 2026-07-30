"use client";

import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from "react";
import { Button, Icon } from "@/components/ui";
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
  const triggerRef = useRef<HTMLButtonElement>(null);
  const dialogRef = useRef<HTMLDivElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const dialogTitleId = useId();
  const searchId = useId();

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return q ? options.filter((o) => o.name.toLowerCase().includes(q)) : options;
  }, [options, search]);

  const selectedOptions = options.filter((o) => selected.includes(o.id));
  const atMax = selected.length >= max;

  useEffect(() => {
    if (!open) return;

    const returnTarget = triggerRef.current;
    const focusFrame = requestAnimationFrame(() => searchRef.current?.focus());
    return () => {
      cancelAnimationFrame(focusFrame);
      returnTarget?.focus();
    };
  }, [open]);

  function closePicker() {
    setOpen(false);
  }

  function handleDialogKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape") {
      event.preventDefault();
      closePicker();
      return;
    }
    if (event.key !== "Tab" || !dialogRef.current) return;

    const focusable = Array.from(
      dialogRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), input:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
      )
    ).filter((element) => element.offsetParent !== null);
    const first = focusable[0];
    const last = focusable.at(-1);
    if (!first || !last) return;

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

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
            className="inline-flex min-h-[var(--tap-min)] items-center gap-1.5 rounded-full bg-primary px-3 text-sm text-primary-ink outline-none transition-[color,background-color,border-color] hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px active:bg-primary/80"
          >
            {o.name}
            <Icon name="x" size={16} />
          </button>
        ))}
        <button
          ref={triggerRef}
          type="button"
          onClick={() => setOpen(true)}
          className="inline-flex min-h-[var(--tap-min)] items-center gap-1 rounded-full border border-dashed border-border px-3 text-sm text-ink-muted outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
        >
          + pilih
        </button>
      </div>

      {open && (
        <div
          className="fixed inset-0 z-50 flex items-end justify-center bg-ink/40 sm:items-center"
          onClick={closePicker}
        >
          <div
            ref={dialogRef}
            role="dialog"
            aria-modal="true"
            aria-labelledby={dialogTitleId}
            className="flex max-h-[70vh] w-full flex-col rounded-t-[var(--radius-lg)] border border-border bg-surface p-4 sm:max-w-md sm:rounded-[var(--radius-lg)]"
            onClick={(e) => e.stopPropagation()}
            onKeyDown={handleDialogKeyDown}
          >
            <div className="mb-3 flex items-center justify-between">
              <h2 id={dialogTitleId} className="text-base font-semibold text-ink">
                Pilih kompetensi (maks {max})
              </h2>
              <button
                type="button"
                onClick={closePicker}
                aria-label="Tutup"
                className="flex min-h-[var(--tap-min)] min-w-[var(--tap-min)] items-center justify-center rounded-[var(--radius-md)] text-ink-muted outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
              >
                <Icon name="x" size={20} />
              </button>
            </div>

            <label htmlFor={searchId} className="sr-only">
              Cari kompetensi
            </label>
            <input
              ref={searchRef}
              id={searchId}
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Cari kompetensi…"
              className="mb-3 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1"
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
                        "flex min-h-[var(--tap-min)] items-center justify-between rounded-[var(--radius-md)] px-3 text-left text-sm outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-[-2px] active:bg-primary-muted disabled:cursor-not-allowed disabled:opacity-[0.55]",
                        isSelected ? "bg-primary-muted text-ink" : "text-ink hover:bg-surface-muted",
                      )}
                    >
                      {o.name}
                      {isSelected && <Icon name="check" size={16} />}
                    </button>
                  );
                })}
              </div>
            </div>

            <Button type="button" size="lg" className="mt-3 w-full" onClick={closePicker}>
              Selesai
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
