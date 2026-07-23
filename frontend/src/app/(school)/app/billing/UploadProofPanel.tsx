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

/**
 * VOK-H6-E2 §3 UploadProofPanel — [GAP dicatat, lihat DECISIONS.md]: AC ticket literal minta
 * "upload bukti transfer (presigned)" pola sama PhotoUploader (pilih file -> presign -> PUT MinIO
 * -> objectKey), TAPI backend TIDAK punya endpoint presign yang bisa diakses TenantAdmin utk
 * bukti invoice — dua endpoint presign yang ADA (/journals/upload-url, /placements/{id}/visits/
 * upload-url) masing2 dikunci StudentSelf & TeacherPlus (scoped ke placement kunjungan), BUKAN
 * dokumen generik TenantAdmin. UploadPaymentProof sendiri (POST /api/invoices/{id}/payment-proof)
 * cuma menerima ObjectKey string jadi (UploadPaymentProofRequest(string ObjectKey)) - TIDAK terima
 * file mentah. Menambah endpoint presign generik ke backend DI LUAR wilayah ticket ini
 * (`frontend/` saja) - maka panel ini menerima ObjectKey secara MANUAL (mis. hasil upload lewat
 * jalur lain / disiapkan admin) alih2 memasang tombol pilih-file yang tak akan pernah berfungsi.
 */
export function UploadProofPanel({ invoice, onUploaded, onCancel }: UploadProofPanelProps) {
  const [objectKey, setObjectKey] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);
    try {
      const updated = await apiClient.post<InvoiceDto>(`/invoices/${invoice.id}/payment-proof`, { objectKey: objectKey.trim() });
      onUploaded(updated);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal mencatat bukti transfer.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface-muted p-3">
      <p className="text-xs text-ink-muted">
        Belum ada jalur unggah file langsung untuk bukti invoice (lihat DECISIONS.md — gap presign) — masukkan kode objek
        penyimpanan bukti transfer secara manual.
      </p>
      <input
        type="text"
        value={objectKey}
        onChange={(e) => setObjectKey(e.target.value)}
        placeholder="mis. bukti-transfer/invoice-xxx.jpg"
        className="h-9 w-full rounded-[var(--radius-md)] border border-border px-3 text-sm outline-none focus:outline-2 focus:outline-primary focus:outline-offset-1"
      />
      {error && <p className="text-xs text-status-red">{error}</p>}
      <div className="flex gap-2">
        <Button variant="primary" size="md" loading={submitting} disabled={objectKey.trim().length === 0} onClick={handleSubmit} className="h-8 px-3 text-xs">
          Simpan Bukti
        </Button>
        <Button variant="secondary" size="md" onClick={onCancel} disabled={submitting} className="h-8 px-3 text-xs">
          Batal
        </Button>
      </div>
    </div>
  );
}
