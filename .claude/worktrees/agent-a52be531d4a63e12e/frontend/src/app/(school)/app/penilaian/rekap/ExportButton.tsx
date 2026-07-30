"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { ExportFormat, type ExportAcceptedDto } from "@/lib/apiTypes";

export interface ExportButtonProps {
  periodId: string;
}

const TOAST_DURATION_MS = 5000;

/**
 * VOK-H5-E2 §3 ExportButton({periodId}) — pilih format -> RequestExport (202, FR-ASM-06) -> toast
 * inline "diproses, cek notifikasi" (TIDAK ada komponen Toast bersama di codebase ini - dicek grep,
 * nol hasil - toast lokal sederhana di sini, bukan sistem baru utk 1 tombol). AC: "tidak ada
 * spinner blocking" - tombol tidak disabled lama/tidak nge-block UI lain, cuma toast singkat lalu
 * balik ke state semula. Link unduh SUNGGUHAN muncul lewat NotificationBell/Panel saat
 * `ExportReady` masuk (lihat NotificationPanel.tsx extractDownloadUrl), BUKAN di sini.
 */
export function ExportButton({ periodId }: ExportButtonProps) {
  const [format, setFormat] = useState<number>(ExportFormat.Xlsx);
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  async function handleExport() {
    setSubmitting(true);
    try {
      await apiClient.post<ExportAcceptedDto>(`/periods/${periodId}/exports`, { format });
      setToast("Export sedang diproses — cek notifikasi 🔔 saat file siap diunduh.");
    } catch (err) {
      setToast(err instanceof ApiError ? err.message : "Gagal meminta export. Coba lagi.");
    } finally {
      setSubmitting(false);
      setTimeout(() => setToast(null), TOAST_DURATION_MS);
    }
  }

  return (
    <div className="flex flex-col items-end gap-1.5">
      <div className="flex items-center gap-2">
        <label className="flex items-center gap-1 text-sm text-ink-muted">
          <select
            value={format}
            onChange={(e) => setFormat(Number(e.target.value))}
            className="h-9 rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
          >
            <option value={ExportFormat.Xlsx}>Excel (.xlsx)</option>
            <option value={ExportFormat.Pdf}>PDF</option>
          </select>
        </label>
        <Button variant="secondary" size="md" loading={submitting} onClick={handleExport}>
          Export Rekap
        </Button>
      </div>
      {toast && <span className="max-w-xs text-right text-xs text-ink-muted">{toast}</span>}
    </div>
  );
}
