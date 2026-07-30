"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
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

  const trimmed = reason.trim();
  const tooShort = trimmed.length > 0 && trimmed.length < MIN_LENGTH;
  const canSubmit = trimmed.length >= MIN_LENGTH && trimmed.length <= MAX_LENGTH && !busy;

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-ink/40 sm:items-center" onClick={onClose}>
      <div
        className="flex w-full flex-col gap-3 rounded-t-[var(--radius-lg)] border border-border bg-surface p-4 sm:max-w-md sm:rounded-[var(--radius-lg)]"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between">
          <h2 className="text-base font-semibold text-ink">Tolak jurnal {studentName}</h2>
          <button type="button" onClick={onClose} aria-label="Tutup" className="p-1 text-ink-muted">
            ✕
          </button>
        </div>

        <div className="flex flex-wrap gap-1.5">
          {QUICK_REASONS.map((q) => (
            <button
              key={q}
              type="button"
              onClick={() => setReason(q)}
              className="rounded-full border border-border px-2.5 py-1 text-xs text-ink hover:bg-surface-muted"
            >
              {q}
            </button>
          ))}
        </div>

        <textarea
          autoFocus
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          maxLength={MAX_LENGTH}
          placeholder="Tulis alasan penolakan (min 5 karakter)..."
          className={cn(
            "min-h-24 rounded-[var(--radius-md)] border px-3 py-2 text-base outline-none resize-y",
            "focus:outline-2 focus:outline-primary focus:outline-offset-1",
            tooShort ? "border-status-amber" : "border-border"
          )}
        />
        <div className="flex items-center justify-between text-xs text-ink-muted">
          <span className={tooShort ? "text-status-amber" : undefined}>
            {tooShort ? `Minimal ${MIN_LENGTH} karakter` : " "}
          </span>
          <span>
            {trimmed.length}/{MAX_LENGTH}
          </span>
        </div>

        <div className="flex gap-2">
          <Button variant="secondary" className="flex-1" onClick={onClose} disabled={busy}>
            Batal
          </Button>
          <Button variant="danger" className="flex-1" onClick={() => onSubmit(trimmed)} disabled={!canSubmit} loading={busy}>
            Tolak Jurnal
          </Button>
        </div>
      </div>
    </div>
  );
}
