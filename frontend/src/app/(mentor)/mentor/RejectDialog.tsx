"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent } from "react";
import { Button, Icon } from "@/components/ui";
import { cn } from "@/lib/cn";

interface RejectDialogProps {
  studentName: string;
  busy: boolean;
  onClose: () => void;
  onSubmit: (reason: string) => void;
}

const QUICK_REASONS = [
  "Teks terlalu singkat, tambah detail",
  "Foto tidak jelas/tidak sesuai",
  "Kompetensi yang dipilih tidak sesuai",
  "Bukan aktivitas hari ini",
];

const MIN_LENGTH = 5;
const MAX_LENGTH = 300;

/** VOK-H3-E2 §2 RejectDialog({journalId, onSubmit}) — alasan wajib, chip alasan cepat (AC <=2mnt). */
export function RejectDialog({ studentName, busy, onClose, onSubmit }: RejectDialogProps) {
  const [reason, setReason] = useState("");
  const dialogRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const dialogTitleId = useId();
  const reasonId = useId();
  const reasonHelpId = useId();

  const trimmed = reason.trim();
  const tooShort = trimmed.length > 0 && trimmed.length < MIN_LENGTH;
  const canSubmit = trimmed.length >= MIN_LENGTH && trimmed.length <= MAX_LENGTH && !busy;

  useEffect(() => {
    const returnTarget = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    const focusFrame = requestAnimationFrame(() => textareaRef.current?.focus());
    return () => {
      cancelAnimationFrame(focusFrame);
      returnTarget?.focus();
    };
  }, []);

  function handleDialogKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape" && !busy) {
      event.preventDefault();
      onClose();
      return;
    }
    if (event.key !== "Tab" || !dialogRef.current) return;

    const focusable = Array.from(
      dialogRef.current.querySelectorAll<HTMLElement>(
        'button:not([disabled]), textarea:not([disabled]), [href], [tabindex]:not([tabindex="-1"])'
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

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-ink/40 sm:items-center" onClick={onClose}>
      <div
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={dialogTitleId}
        className="flex w-full flex-col gap-3 rounded-t-[var(--radius-lg)] border border-border bg-surface p-4 sm:max-w-md sm:rounded-[var(--radius-lg)]"
        onClick={(e) => e.stopPropagation()}
        onKeyDown={handleDialogKeyDown}
      >
        <div className="flex items-center justify-between">
          <h2 id={dialogTitleId} className="text-base font-semibold text-ink">
            Tolak jurnal {studentName}
          </h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Tutup"
            className="flex min-h-[var(--tap-min)] min-w-[var(--tap-min)] items-center justify-center rounded-[var(--radius-md)] text-ink-muted outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
          >
            <Icon name="x" size={20} />
          </button>
        </div>

        <div className="flex flex-wrap gap-1.5">
          {QUICK_REASONS.map((q) => (
            <button
              key={q}
              type="button"
              disabled={busy}
              onClick={() => setReason(q)}
              className="min-h-[var(--tap-min)] rounded-full border border-border px-3 py-1 text-xs text-ink outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted disabled:cursor-not-allowed disabled:bg-surface-muted disabled:opacity-[0.55]"
            >
              {q}
            </button>
          ))}
        </div>

        <label htmlFor={reasonId} className="sr-only">
          Alasan penolakan
        </label>
        <textarea
          ref={textareaRef}
          id={reasonId}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          maxLength={MAX_LENGTH}
          disabled={busy}
          aria-invalid={tooShort}
          aria-describedby={reasonHelpId}
          placeholder="Tulis alasan penolakan (minimal 5 karakter)…"
          className={cn(
            "min-h-24 resize-y rounded-[var(--radius-md)] border px-3 py-2 text-base outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1 disabled:cursor-not-allowed disabled:bg-surface-muted disabled:opacity-[0.55]",
            tooShort ? "border-status-amber" : "border-border"
          )}
        />
        <div id={reasonHelpId} className="flex min-h-[1.25rem] items-center justify-between text-xs text-ink-muted">
          <span className={tooShort ? "text-status-amber" : undefined}>
            {tooShort ? `Minimal ${MIN_LENGTH} karakter` : " "}
          </span>
          <span>
            {trimmed.length}/{MAX_LENGTH}
          </span>
        </div>

        <div className="flex flex-col gap-2 min-[24rem]:flex-row">
          <Button
            variant="secondary"
            size="lg"
            className="w-full whitespace-nowrap min-[24rem]:w-auto min-[24rem]:flex-1"
            onClick={onClose}
            disabled={busy}
          >
            Batal
          </Button>
          <Button
            variant="danger"
            size="lg"
            className="w-full whitespace-nowrap min-[24rem]:w-auto min-[24rem]:flex-1"
            onClick={() => onSubmit(trimmed)}
            disabled={!canSubmit}
            loading={busy}
          >
            Tolak Jurnal
          </Button>
        </div>
      </div>
    </div>
  );
}
