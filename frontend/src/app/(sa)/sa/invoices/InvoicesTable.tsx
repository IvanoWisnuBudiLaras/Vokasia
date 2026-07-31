"use client";

import { useState } from "react";
import { Button, StatusBadge, TableExportToolbar, Tooltip } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { InvoiceStatus, type InvoiceDto } from "@/lib/apiTypes";
import { downloadSignedInvoicePdf } from "@/lib/invoicePdfGenerator";

export interface InvoicesTableProps {
  initialInvoices: InvoiceDto[];
}

function statusBadge(status: number) {
  if (status === InvoiceStatus.Paid) return <StatusBadge status="green" label="Lunas" />;
  if (status === InvoiceStatus.ProofUploaded) return <StatusBadge status="amber" label="Bukti Diunggah" />;
  return <StatusBadge status="red" label="Belum Bayar" />;
}

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
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold text-ink">Invoices Platform</h1>

        <TableExportToolbar
          data={invoices}
          filename="daftar_invoice_vokasia"
          title="Daftar Invoice & Resi Pembayaran — Vokasia Platform"
          columns={[
            { key: "periodMonth", label: "Periode", format: (val) => new Date(val).toLocaleDateString("id-ID", { month: "long", year: "numeric" }) },
            { key: "amount", label: "Jumlah (Rp)", format: (val) => `Rp ${val.toLocaleString("id-ID")}` },
            { key: "id", label: "No. Resi Bukti", format: (val, row) => `RESI-VOK-${new Date(row.periodMonth).getFullYear()}${String(new Date(row.periodMonth).getMonth() + 1).padStart(2, "0")}-${val.slice(0, 5).toUpperCase()}` },
            { key: "status", label: "Status", format: (val) => val === InvoiceStatus.Paid ? "Lunas" : val === InvoiceStatus.ProofUploaded ? "Bukti Diunggah" : "Belum Bayar" },
          ]}
        />
      </div>

      {error && <p className="text-sm text-status-red">{error}</p>}

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border bg-surface">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted border-b border-border font-medium text-ink-muted">
            <tr>
              <th className="p-3">Periode</th>
              <th className="p-3">No. Resi Bukti</th>
              <th className="p-3">Jumlah</th>
              <th className="p-3">Bukti Transfer</th>
              <th className="p-3">Status</th>
              <th className="p-3">Aksi & Nota PDF</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {invoices.map((inv) => {
              const resiNo = `RESI-VOK-${new Date(inv.periodMonth).getFullYear()}${String(new Date(inv.periodMonth).getMonth() + 1).padStart(2, "0")}-${inv.id.slice(0, 5).toUpperCase()}`;
              return (
                <tr key={inv.id} className="hover:bg-surface-muted/50">
                  <td className="p-3 font-medium text-ink">
                    {new Date(inv.periodMonth).toLocaleDateString("id-ID", { month: "long", year: "numeric" })}
                  </td>
                  <td className="p-3 font-mono text-xs font-semibold text-primary">{resiNo}</td>
                  <td className="p-3 text-ink">Rp {inv.amount.toLocaleString("id-ID")}</td>
                  <td className="p-3 text-xs text-ink-muted">{inv.proofKey ?? "—"}</td>
                  <td className="p-3">{statusBadge(inv.status)}</td>
                  <td className="p-3">
                    <div className="flex flex-wrap items-center gap-2">
                      {inv.status === InvoiceStatus.ProofUploaded && (
                        <Tooltip content="Konfirmasi pembayaran bukti transfer dari sekolah ini">
                          <Button
                            variant="primary"
                            size="md"
                            loading={confirmingId === inv.id}
                            onClick={() => handleConfirm(inv.id)}
                            className="px-3 text-xs"
                          >
                            Konfirmasi Lunas
                          </Button>
                        </Tooltip>
                      )}

                      {inv.status === InvoiceStatus.Paid && (
                        <Tooltip content="Cetak atau simpan Nota Pembayaran Resmi PDF yang sudah ditandatangani">
                          <Button
                            variant="secondary"
                            size="md"
                            onClick={() => downloadSignedInvoicePdf(inv)}
                            className="px-3 text-xs text-primary"
                          >
                            Unduh Nota PDF
                          </Button>
                        </Tooltip>
                      )}

                      {inv.status === InvoiceStatus.Issued && (
                        <span className="text-xs text-ink-muted">Menunggu bukti transfer</span>
                      )}
                    </div>
                  </td>
                </tr>
              );
            })}
            {invoices.length === 0 && (
              <tr>
                <td colSpan={6} className="p-6 text-center text-sm text-ink-muted">
                  Belum ada invoice.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
