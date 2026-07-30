"use client";

import { useState } from "react";
import { Button, StatusBadge } from "@/components/ui";
import { InvoiceStatus, type InvoiceDto } from "@/lib/apiTypes";
import { UploadProofPanel } from "./UploadProofPanel";

export interface BillingTableProps {
  initialInvoices: InvoiceDto[];
}

function statusBadge(status: number) {
  if (status === InvoiceStatus.Paid) return <StatusBadge status="green" label="Lunas" />;
  if (status === InvoiceStatus.ProofUploaded) return <StatusBadge status="amber" label="Menunggu Konfirmasi SA" />;
  return <StatusBadge status="red" label="Belum Bayar" />;
}

/**
 * VOK-H6-E2 §3 app/billing/page.tsx — TenantAdmin: daftar invoice tenant sendiri (ListMyInvoices)
 * + upload bukti transfer per invoice Issued (lihat UploadProofPanel utk gap presign). Setelah
 * ProofUploaded, status berikutnya (Paid) HANYA berubah lewat ConfirmPayment sisi SA (sa/invoices)
 * — halaman ini murni read+upload, tak ada aksi konfirmasi lunas sendiri (sesuai FR-BIL-01..03:
 * TenantAdmin tak berwenang menyatakan pembayarannya sendiri lunas).
 */
export function BillingTable({ initialInvoices }: BillingTableProps) {
  const [invoices, setInvoices] = useState(initialInvoices);
  const [uploadingId, setUploadingId] = useState<string | null>(null);

  function handleUploaded(updated: InvoiceDto) {
    setInvoices((prev) => prev.map((inv) => (inv.id === updated.id ? updated : inv)));
    setUploadingId(null);
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-ink">Billing</h1>

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted">
            <tr>
              <th className="p-3 font-medium text-ink">Periode</th>
              <th className="p-3 font-medium text-ink">Jumlah</th>
              <th className="p-3 font-medium text-ink">Status</th>
              <th className="p-3 font-medium text-ink">Aksi</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((inv) => (
              <tr key={inv.id} className="border-t border-border">
                <td className="p-3 text-ink">
                  {new Date(inv.periodMonth).toLocaleDateString("id-ID", { month: "long", year: "numeric" })}
                </td>
                <td className="p-3 text-ink">Rp {inv.amount.toLocaleString("id-ID")}</td>
                <td className="p-3">{statusBadge(inv.status)}</td>
                <td className="p-3">
                  {inv.status === InvoiceStatus.Issued &&
                    (uploadingId === inv.id ? (
                      <UploadProofPanel invoice={inv} onUploaded={handleUploaded} onCancel={() => setUploadingId(null)} />
                    ) : (
                      <Button variant="primary" size="md" onClick={() => setUploadingId(inv.id)} className="h-8 px-3 text-xs">
                        Unggah Bukti Transfer
                      </Button>
                    ))}
                  {inv.status === InvoiceStatus.ProofUploaded && (
                    <span className="text-xs text-ink-muted">Bukti terkirim — menunggu konfirmasi Super Admin.</span>
                  )}
                  {inv.status === InvoiceStatus.Paid && <span className="text-xs text-status-green">Sudah lunas.</span>}
                </td>
              </tr>
            ))}
            {invoices.length === 0 && (
              <tr>
                <td colSpan={4} className="p-6 text-center text-sm text-ink-muted">
                  Belum ada invoice untuk sekolah ini.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
