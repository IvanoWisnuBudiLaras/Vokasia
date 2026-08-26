"use client";

import { useState } from "react";
import { Button, StatusBadge } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import {
  InvoiceStatus,
  SubscriptionStatus,
  type BankTransferInstructionsDto,
  type InvoiceDto,
  type SubscriptionDto,
} from "@/lib/apiTypes";
import { UploadProofPanel } from "./UploadProofPanel";

export interface BillingTableProps {
  initialInvoices: InvoiceDto[];
  initialSubscription?: SubscriptionDto | null;
  bankInstructions?: BankTransferInstructionsDto | null;
}

function subscriptionBadge(status: number) {
  if (status === SubscriptionStatus.Active) return <StatusBadge status="green" label="Aktif" />;
  if (status === SubscriptionStatus.Suspended) return <StatusBadge status="red" label="Ditangguhkan" />;
  if (status === SubscriptionStatus.Expired) return <StatusBadge status="red" label="Kadaluarsa" />;
  return <StatusBadge status="amber" label="Menunggu Pembayaran" />;
}

function invoiceBadge(status: number) {
  if (status === InvoiceStatus.Paid) return <StatusBadge status="green" label="Lunas" />;
  if (status === InvoiceStatus.PendingVerification) return <StatusBadge status="amber" label="Menunggu Verifikasi" />;
  if (status === InvoiceStatus.Rejected) return <StatusBadge status="red" label="Pembayaran Ditolak" />;
  if (status === InvoiceStatus.Expired) return <StatusBadge status="red" label="Kadaluarsa" />;
  return <StatusBadge status="amber" label="Belum Bayar" />;
}

export function BillingTable({ initialInvoices, initialSubscription, bankInstructions }: BillingTableProps) {
  const [invoices, setInvoices] = useState(initialInvoices);
  const [subscription] = useState(initialSubscription);
  const [uploadingId, setUploadingId] = useState<string | null>(null);
  const [downloadingProofId, setDownloadingProofId] = useState<string | null>(null);

  function handleUploaded(updated: InvoiceDto) {
    setInvoices((prev) => prev.map((inv) => (inv.id === updated.id ? updated : inv)));
    setUploadingId(null);
  }

  async function handleViewProof(invoiceId: string) {
    setDownloadingProofId(invoiceId);
    try {
      const res = await apiClient.get<{ downloadUrl: string }>(`/invoices/${invoiceId}/payment-proof/download-url`);
      window.open(res.downloadUrl, "_blank");
    } catch {
      alert("Gagal memuat berkas bukti pembayaran.");
    } finally {
      setDownloadingProofId(null);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      {/* Subscription Card */}
      <div className="rounded-[var(--radius-lg)] border border-border bg-surface p-5 shadow-xs">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-border pb-3">
          <div>
            <h2 className="text-lg font-semibold text-ink">Status Langganan</h2>
            <p className="text-xs text-ink-muted">Informasi paket dan masa aktif langganan sekolah</p>
          </div>
          {subscription && subscriptionBadge(subscription.status)}
        </div>

        {subscription ? (
          <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <div className="rounded-[var(--radius-md)] bg-surface-muted p-3">
              <span className="text-xs text-ink-muted">Paket</span>
              <p className="font-semibold text-ink">{subscription.planName}</p>
            </div>
            <div className="rounded-[var(--radius-md)] bg-surface-muted p-3">
              <span className="text-xs text-ink-muted">Kapasitas Siswa</span>
              <p className="font-semibold text-ink">{subscription.studentCapacity} Siswa</p>
            </div>
            <div className="rounded-[var(--radius-md)] bg-surface-muted p-3">
              <span className="text-xs text-ink-muted">Masa Aktif</span>
              <p className="text-xs font-medium text-ink">
                {new Date(subscription.startsAt).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" })}
                {" — "}
                {new Date(subscription.endsAt).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" })}
              </p>
            </div>
            <div className="rounded-[var(--radius-md)] bg-surface-muted p-3">
              <span className="text-xs text-ink-muted">Biaya Tahunan</span>
              <p className="font-semibold text-brand-action">Rp {subscription.annualPrice.toLocaleString("id-ID")}/tahun</p>
            </div>
          </div>
        ) : (
          <p className="mt-3 text-sm text-ink-muted">Belum ada data langganan aktif. Silakan lunasi tagihan di bawah.</p>
        )}
      </div>

      {/* Invoices List */}
      <div className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-ink">Daftar Tagihan & Pembayaran</h2>

        <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border bg-surface shadow-xs">
          <table className="w-full text-left text-sm">
            <thead className="border-b border-border bg-surface-muted font-medium text-ink-muted">
              <tr>
                <th className="p-3">No. Tagihan</th>
                <th className="p-3">Paket</th>
                <th className="p-3">Jatuh Tempo</th>
                <th className="p-3">Jumlah</th>
                <th className="p-3">Status</th>
                <th className="p-3">Aksi</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {invoices.map((inv) => (
                <tr key={inv.id} className="hover:bg-surface-muted/50">
                  <td className="p-3 font-mono text-xs font-semibold text-primary">{inv.invoiceNumber}</td>
                  <td className="p-3 text-ink">{inv.planName}</td>
                  <td className="p-3 text-xs text-ink-muted">
                    {new Date(inv.dueAt).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" })}
                  </td>
                  <td className="p-3 font-medium text-ink">Rp {inv.amount.toLocaleString("id-ID")}</td>
                  <td className="p-3">
                    <div className="flex flex-col gap-1">
                      {invoiceBadge(inv.status)}
                      {inv.status === InvoiceStatus.Rejected && inv.rejectionReason && (
                        <span className="text-xs text-status-red">Alasan: {inv.rejectionReason}</span>
                      )}
                    </div>
                  </td>
                  <td className="p-3">
                    {uploadingId === inv.id ? (
                      <UploadProofPanel
                        invoice={inv}
                        bankInstructions={bankInstructions}
                        onUploaded={handleUploaded}
                        onCancel={() => setUploadingId(null)}
                      />
                    ) : (
                      <div className="flex flex-wrap items-center gap-2">
                        {(inv.status === InvoiceStatus.Unpaid || inv.status === InvoiceStatus.Rejected) && (
                          <Button
                            variant="primary"
                            size="md"
                            onClick={() => setUploadingId(inv.id)}
                            className="px-3 text-xs"
                          >
                            {inv.status === InvoiceStatus.Rejected ? "Unggah Ulang Bukti" : "Bayar / Unggah Bukti"}
                          </Button>
                        )}
                        {inv.status === InvoiceStatus.PendingVerification && (
                          <div className="flex items-center gap-2">
                            <span className="text-xs text-ink-muted">Menunggu verifikasi Super Admin</span>
                            {inv.proofKey && (
                              <Button
                                variant="secondary"
                                size="md"
                                loading={downloadingProofId === inv.id}
                                onClick={() => handleViewProof(inv.id)}
                                className="px-2 text-xs"
                              >
                                Lihat Bukti
                              </Button>
                            )}
                          </div>
                        )}
                        {inv.status === InvoiceStatus.Paid && (
                          <div className="flex items-center gap-2">
                            <span className="text-xs text-status-green">Lunas</span>
                            {inv.proofKey && (
                              <Button
                                variant="secondary"
                                size="md"
                                loading={downloadingProofId === inv.id}
                                onClick={() => handleViewProof(inv.id)}
                                className="px-2 text-xs"
                              >
                                Lihat Bukti
                              </Button>
                            )}
                          </div>
                        )}
                      </div>
                    )}
                  </td>
                </tr>
              ))}
              {invoices.length === 0 && (
                <tr>
                  <td colSpan={6} className="p-6 text-center text-sm text-ink-muted">
                    Belum ada tagihan untuk sekolah ini.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
