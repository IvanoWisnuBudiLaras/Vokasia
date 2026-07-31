"use client";

import { useState } from "react";
import { Button, Icon } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { ExportFormat, type ExportAcceptedDto, type RecapRowDto } from "@/lib/apiTypes";

export interface ExportButtonProps {
  periodId: string;
  rows?: RecapRowDto[];
}

const TOAST_DURATION_MS = 5000;

export function ExportButton({ periodId, rows }: ExportButtonProps) {
  const [format, setFormat] = useState<number>(ExportFormat.Xlsx);
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  function exportDirectlyToCsv() {
    if (!rows || rows.length === 0) return false;
    const headers = ["Nama Siswa", "Mitra DUDI", "Rata-Rata Mentor", "Rata-Rata Guru", "Nilai Final", "Status"];
    const csvRows = [headers.join(",")];

    for (const r of rows) {
      const line = [
        `"${(r.studentName || "").replace(/"/g, '""')}"`,
        `"${(r.companyName || "").replace(/"/g, '""')}"`,
        r.mentorAvg !== null ? r.mentorAvg.toFixed(2) : "—",
        r.teacherAvg !== null ? r.teacherAvg.toFixed(2) : "—",
        r.finalScore !== null ? r.finalScore.toFixed(2) : "—",
        `"${r.status}"`
      ];
      csvRows.push(line.join(","));
    }

    const blob = new Blob([csvRows.join("\n")], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `rekap_nilai_pkl_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    return true;
  }

  async function handleExport() {
    setSubmitting(true);
    try {
      // 1. Instant client-side download if row data is available
      if (format === ExportFormat.Xlsx && rows && rows.length > 0) {
        exportDirectlyToCsv();
        setToast("File Excel (.csv) berhasil diunduh langsung!");
      } else {
        // 2. Background job export request for heavy background PDF/Excel worker
        await apiClient.post<ExportAcceptedDto>(`/periods/${periodId}/exports`, { format });
        setToast("Export sedang diproses — cek notifikasi saat file siap diunduh.");
      }
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
            className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
          >
            <option value={ExportFormat.Xlsx}>Excel (.xlsx / .csv)</option>
            <option value={ExportFormat.Pdf}>Dokumen PDF</option>
          </select>
        </label>
        <Button variant="secondary" size="lg" loading={submitting} onClick={handleExport}>
          <Icon name="download" size={16} /> Unduh Data
        </Button>
      </div>
      {toast && <span className="max-w-xs text-right text-xs font-medium text-status-green">{toast}</span>}
    </div>
  );
}
