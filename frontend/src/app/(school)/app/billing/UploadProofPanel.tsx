"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { InvoiceDto } from "@/lib/apiTypes";

interface UploadProofPanelProps {
  invoice: InvoiceDto;
  onUploaded: (updated: InvoiceDto) => void;
  onCancel: () => void;
}

export function buildPaymentProofUploadRequest(file: File) {
  return {
    fileName: file.name,
    contentType: file.type,
    sizeBytes: file.size,
  };
}

/** Browser flow: choose file -> scoped presigned PUT -> submit backend-generated object key. */
export function UploadProofPanel({ invoice, onUploaded, onCancel }: UploadProofPanelProps) {
  const [file, setFile] = useState<File | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit() {
    if (!file) return;
    setSubmitting(true);
    setError(null);
    try {
      const upload = await apiClient.post<{ uploadUrl: string; objectKey: string }>(
        `/invoices/${invoice.id}/payment-proof/upload-url`,
        buildPaymentProofUploadRequest(file),
      );
      const put = await fetch(upload.uploadUrl, { method: "PUT", headers: { "Content-Type": file.type }, body: file });
      if (!put.ok) throw new Error("Upload bukti gagal.");
      const updated = await apiClient.post<InvoiceDto>(`/invoices/${invoice.id}/payment-proof`, { objectKey: upload.objectKey });
      onUploaded(updated);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal mengirim bukti transfer. Coba lagi.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface-muted p-3">
      <p className="text-xs text-ink-muted">Pilih berkas bukti transfer. Kode objek dibuat dan dikendalikan oleh server.</p>
      <input type="file" accept="image/jpeg,image/png,application/pdf" onChange={(e) => setFile(e.target.files?.[0] ?? null)} aria-label="Bukti transfer" className="h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border px-3 text-sm" />
      {error && <p role="alert" className="text-xs text-status-red">{error}</p>}
      <div className="flex gap-2">
        <Button variant="primary" size="md" loading={submitting} disabled={!file} onClick={handleSubmit} className="px-3 text-xs">Simpan Bukti</Button>
        <Button variant="secondary" size="md" onClick={onCancel} disabled={submitting} className="px-3 text-xs">Batal</Button>
      </div>
    </div>
  );
}
