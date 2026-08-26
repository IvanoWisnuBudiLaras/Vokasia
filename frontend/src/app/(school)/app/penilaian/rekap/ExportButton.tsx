"use client";

import { useState } from "react";
import { Button, Icon } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { ExportFormat, type ExportAcceptedDto } from "@/lib/apiTypes";

export interface ExportButtonProps {
  periodId: string;
}

const TOAST_DURATION_MS = 5000;

export function ExportButton({ periodId }: ExportButtonProps) {
  const [submitting, setSubmitting] = useState(false);
  const [toast, setToast] = useState<string | null>(null);

  async function handleExport(format: number, label: string) {
    setSubmitting(true);
    try {
      await apiClient.post<ExportAcceptedDto>(`/periods/${periodId}/exports`, { format });
      setToast(`${label} sedang disiapkan. Cek notifikasi saat file siap diunduh.`);
    } catch (err) {
      setToast(err instanceof ApiError ? err.message : "Gagal meminta export. Coba lagi.");
    } finally {
      setSubmitting(false);
      setTimeout(() => setToast(null), TOAST_DURATION_MS);
    }
  }

  return (
    <div className="flex flex-col items-end gap-1.5">
      <div className="flex flex-wrap items-center justify-end gap-2">
        <Button variant="secondary" size="lg" loading={submitting} onClick={() => void handleExport(ExportFormat.Pdf, "PDF")}>
          <Icon name="download" size={16} /> Unduh PDF
        </Button>
        <Button variant="secondary" size="lg" loading={submitting} onClick={() => void handleExport(ExportFormat.Xlsx, "XLSX")}>
          <Icon name="download" size={16} /> Unduh XLSX
        </Button>
      </div>
      {toast && <span className="max-w-xs text-right text-xs font-medium text-status-green">{toast}</span>}
    </div>
  );
}
