"use client";

import { useState } from "react";
import { Button, StatusBadge } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { InvoiceStatus, type InvoiceDto } from "@/lib/apiTypes";

export interface InvoicesTableProps {
  initialInvoices: InvoiceDto[];
}

function statusBadge(status: number) {
  if (status === InvoiceStatus.Paid) return <StatusBadge status="green" label="Lunas" />;
  if (status === InvoiceStatus.ProofUploaded) return <StatusBadge status="amber" label="Bukti Diunggah" />;
  return <StatusBadge status="red" label="Belum Bayar" />;
}

/**
 * VOK-H6-E2 §1 sa/invoices/page.tsx — daftar invoice SEMUA tenant + ConfirmPayment. [GAP dicatat]:
 * "lihat bukti transfer (preview objek)" — backend TIDAK punya endpoint presigned-GET generik utk
 * ProofKey sembarang (hanya GetCertificate yang presign, khusus objek sertifikat sendiri) - preview
 * gambar butuh endpoint backend baru, DI LUAR wilayah ticket ini (`frontend/` saja). Ditampilkan
 * ProofKey sbg teks (bukti path tersimpan) dgn catatan gap, bukan <img> ke URL yang tak akan pernah termuat.
 */
export function InvoicesTable({ initialInvoices }: InvoicesTableProps) {
  const [invoices, setInvoices] = useState(initialInvoices);
  const [confirmingId, setConfirmingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    const data = await apiClient.get<InvoiceDto[]>("/sa/invoices");
    setInvoices(data);
  }

  async function handleConfirm(id: string) {
    setConfirmingId(id);
    setError(null);
    try {
      await apiClient.post(`/sa/invoices/${id}/confirm-payment`);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal konfirmasi pembayaran.");
    } finally {
      setConfirmingId(null);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-ink">Invoices</h1>
      {error && <p className="text-sm text-status-red">{error}</p>}

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted">
            <tr>
              <th className="p-3 font-medium text-ink">Periode</th>
              <th className="p-3 font-medium text-ink">Jumlah</th>
              <th className="p-3 font-medium text-ink">Bukti Transfer</th>
              <th className="p-3 font-medium text-ink">Status</th>
              <th className="p-3 font-medium text-ink">Aksi</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((inv) => (
              <tr key={inv.id} className="border-t border-border">
                <td className="p-3 text-ink">{new Date(inv.periodMonth).toLocaleDateString("id-ID", { month: "long", year: "numeric" })}</td>
                <td className="p-3 text-ink">Rp {inv.amount.toLocaleString("id-ID")}</td>
                <td className="p-3 text-xs text-ink-muted">{inv.proofKey ?? "—"}</td>
                <td className="p-3">{statusBadge(inv.status)}</td>
                <td className="p-3">
                  {inv.status === InvoiceStatus.ProofUploaded && (
                    <Button variant="primary" size="md" loading={confirmingId === inv.id} onClick={() => handleConfirm(inv.id)} className="px-3 text-xs">
                      Konfirmasi Lunas
                    </Button>
                  )}
                  {inv.status === InvoiceStatus.Issued && <span className="text-xs text-ink-muted">Menunggu bukti transfer</span>}
                </td>
              </tr>
            ))}
            {invoices.length === 0 && (
              <tr>
                <td colSpan={5} className="p-6 text-center text-sm text-ink-muted">Belum ada invoice.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
