"use client";

import { useState } from "react";
import { Button, Input } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { useFormDraft } from "@/lib/useFormDraft";
import type { BankTransferInstructionsDto, InvoiceDto } from "@/lib/apiTypes";

interface UploadProofPanelProps {
  invoice: InvoiceDto;
  bankInstructions?: BankTransferInstructionsDto | null;
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

/** Browser flow: view instructions -> choose file -> presigned PUT -> submit backend-generated object key + optional note. */
export function UploadProofPanel({ invoice, bankInstructions, onUploaded, onCancel }: UploadProofPanelProps) {
  const [file, setFile] = useState<File | null>(null);
  const { values, updateField, clearDraft } = useFormDraft(`proof_${invoice.id}`, { note: "" });
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
      const updated = await apiClient.post<InvoiceDto>(`/invoices/${invoice.id}/proof`, {
        objectKey: upload.objectKey,
        note: values.note.trim() || undefined,
      });

      clearDraft();
      onUploaded(updated);
    } catch (err) {
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-border bg-surface-muted p-4">
      {bankInstructions && (
        <div className="rounded-[var(--radius-md)] border border-brand-accent/20 bg-brand-soft p-3 text-xs text-ink">
          <p className="font-semibold text-brand-action">Instruksi Transfer Manual</p>
          <div className="mt-1 grid grid-cols-1 gap-1 sm:grid-cols-2">
            <div>
              <span className="text-ink-muted">Bank:</span> <span className="font-medium">{bankInstructions.bankName}</span>
            </div>
            <div>
              <span className="text-ink-muted">No. Rekening:</span> <span className="font-mono font-medium">{bankInstructions.accountNumber}</span>
            </div>
            <div>
              <span className="text-ink-muted">Atas Nama:</span> <span className="font-medium">{bankInstructions.accountHolder}</span>
            </div>
            <div>
              <span className="text-ink-muted">No. Tagihan:</span> <span className="font-mono font-medium">{invoice.invoiceNumber}</span>
            </div>
            <div className="sm:col-span-2">
              <span className="text-ink-muted">Total Transfer:</span> <span className="font-semibold text-brand-action">Rp {invoice.amount.toLocaleString("id-ID")}</span>
            </div>
          </div>
        </div>
      )}

      <div>
        <label htmlFor="proof-file" className="block text-xs font-medium text-ink">
          Unggah Bukti Transfer (JPG, PNG, atau PDF, maks 10 MB)
        </label>
        <input
          id="proof-file"
          type="file"
          accept="image/jpeg,image/png,application/pdf"
          onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          aria-label="Bukti transfer"
          className="mt-1 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 py-1 text-sm file:mr-3 file:rounded file:border-0 file:bg-brand-soft file:px-2 file:py-1 file:text-xs file:font-semibold file:text-brand-action"
        />
      </div>

      <Input
        label="Catatan Pembayaran (Opsional)"
        placeholder="Contoh: Transfer atas nama Bendahara SMK"
        value={values.note}
        onChange={(e) => updateField("note", e.target.value)}
        disabled={submitting}
        maxLength={200}
      />

      {error && <p role="alert" className="text-xs text-status-red">{error}</p>}

      <div className="flex gap-2 pt-1">
        <Button variant="primary" size="md" loading={submitting} disabled={!file} onClick={handleSubmit} className="px-4 text-xs">
          Kirim untuk Diverifikasi
        </Button>
        <Button variant="secondary" size="md" onClick={onCancel} disabled={submitting} className="px-4 text-xs">
          Batal
        </Button>
      </div>
    </div>
  );
}
