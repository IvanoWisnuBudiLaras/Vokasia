"use client";

import { useState } from "react";
import { Button, StatusBadge, TableExportToolbar, Input } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { InvoiceStatus, type InvoiceDto, type PaymentSubmissionDto } from "@/lib/apiTypes";

export interface InvoicesTableProps {
  initialInvoices: InvoiceDto[];
}

function statusBadge(status: number) {
  if (status === InvoiceStatus.Paid) return <StatusBadge status="green" label="Lunas" />;
  if (status === InvoiceStatus.PendingVerification) return <StatusBadge status="amber" label="Menunggu Verifikasi" />;
  if (status === InvoiceStatus.Rejected) return <StatusBadge status="red" label="Pembayaran Ditolak" />;
  if (status === InvoiceStatus.Expired) return <StatusBadge status="red" label="Kadaluarsa" />;
  return <StatusBadge status="red" label="Belum Bayar" />;
}

interface InvoiceDetailState {
  invoice: InvoiceDto;
  schoolName: string;
  submissions: PaymentSubmissionDto[];
}

export function InvoicesTable({ initialInvoices }: InvoicesTableProps) {
  const [invoices, setInvoices] = useState(initialInvoices);
  const [selectedInvoice, setSelectedInvoice] = useState<InvoiceDetailState | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [filterStatus, setFilterStatus] = useState<number | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [actionBusy, setActionBusy] = useState(false);
  const [rejecting, setRejecting] = useState(false);
  const [rejectionReason, setRejectionReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [downloadingProof, setDownloadingProof] = useState(false);

  async function refresh() {
    const data = await apiClient.get<InvoiceDto[]>("/sa/invoices");
    setInvoices(data);
    if (selectedId) {
      await loadDetail(selectedId);
    }
  }

  async function loadDetail(id: string) {
    setLoadingDetail(true);
    setError(null);
    setSelectedId(id);
    try {
      const detail = await apiClient.get<InvoiceDetailState>(`/sa/invoices/${id}`);
      setSelectedInvoice(detail);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal memuat detail invoice.");
      setSelectedInvoice(null);
    } finally {
      setLoadingDetail(false);
    }
  }

  async function handleConfirm(id: string) {
    setActionBusy(true);
    setError(null);
    try {
      await apiClient.post(`/sa/invoices/${id}/confirm-payment`);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal konfirmasi pembayaran.");
    } finally {
      setActionBusy(false);
    }
  }

  async function handleReject(id: string) {
    if (!rejectionReason.trim()) {
      setError("Alasan penolakan wajib diisi.");
      return;
    }
    setActionBusy(true);
    setError(null);
    try {
      await apiClient.post(`/sa/invoices/${id}/reject-payment`, { reason: rejectionReason.trim() });
      setRejecting(false);
      setRejectionReason("");
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menolak pembayaran.");
    } finally {
      setActionBusy(false);
    }
  }

  async function handleViewProof(invoiceId: string) {
    setDownloadingProof(true);
    try {
      const res = await apiClient.get<{ downloadUrl: string }>(`/sa/invoices/${invoiceId}/payment-proof/download-url`);
      window.open(res.downloadUrl, "_blank");
    } catch {
      alert("Gagal memuat berkas bukti pembayaran.");
    } finally {
      setDownloadingProof(false);
    }
  }

  const filteredInvoices = filterStatus !== null
    ? invoices.filter((inv) => inv.status === filterStatus)
    : invoices;

  return (
    <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
      {/* Main Table Area */}
      <div className="flex flex-1 flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-xl font-semibold text-ink">Invoices & Verifikasi Platform</h1>
            <p className="text-xs text-ink-muted">Kelola persetujuan transfer manual untuk langganan sekolah</p>
          </div>

          <div className="flex items-center gap-3">
            <select
              value={filterStatus ?? ""}
              onChange={(e) => setFilterStatus(e.target.value === "" ? null : Number(e.target.value))}
              className="rounded-[var(--radius-md)] border border-border bg-surface px-3 py-1.5 text-xs text-ink"
              aria-label="Filter status tagihan"
            >
              <option value="">Semua Status</option>
              <option value={InvoiceStatus.Unpaid}>Belum Bayar</option>
              <option value={InvoiceStatus.PendingVerification}>Menunggu Verifikasi</option>
              <option value={InvoiceStatus.Paid}>Lunas</option>
              <option value={InvoiceStatus.Rejected}>Ditolak</option>
              <option value={InvoiceStatus.Expired}>Kadaluarsa</option>
            </select>

            <TableExportToolbar
              data={filteredInvoices}
              filename="daftar_invoice_vokasia"
              title="Daftar Invoice & Resi Pembayaran — Vokasia Platform"
              columns={[
                { key: "invoiceNumber", label: "No. Tagihan" },
                { key: "planName", label: "Paket" },
                { key: "periodMonth", label: "Periode", format: (val) => new Date(typeof val === "string" || typeof val === "number" ? val : Date.now()).toLocaleDateString("id-ID", { month: "long", year: "numeric" }) },
                { key: "amount", label: "Jumlah (Rp)", format: (val) => `Rp ${typeof val === "number" ? val.toLocaleString("id-ID") : "-"}` },
                { key: "status", label: "Status", format: (val) => val === InvoiceStatus.Paid ? "Lunas" : val === InvoiceStatus.PendingVerification ? "Menunggu Verifikasi" : val === InvoiceStatus.Rejected ? "Ditolak" : "Belum Bayar" },
              ]}
            />
          </div>
        </div>

        {error && <p className="text-sm text-status-red">{error}</p>}

        <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border bg-surface shadow-xs">
          <table className="w-full text-left text-sm">
            <thead className="bg-surface-muted border-b border-border font-medium text-ink-muted">
              <tr>
                <th className="p-3">No. Tagihan</th>
                <th className="p-3">Paket</th>
                <th className="p-3">Jumlah</th>
                <th className="p-3">Status</th>
                <th className="p-3">Aksi</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {filteredInvoices.map((inv) => (
                <tr
                  key={inv.id}
                  className={`cursor-pointer hover:bg-surface-muted/50 ${selectedId === inv.id ? "bg-brand-soft" : ""}`}
                  onClick={() => loadDetail(inv.id)}
                >
                  <td className="p-3 font-mono text-xs font-semibold text-primary">{inv.invoiceNumber}</td>
                  <td className="p-3 text-ink">
                    <div>{inv.planName}</div>
                    <span className="text-xs text-ink-muted">
                      {new Date(inv.periodMonth).toLocaleDateString("id-ID", { month: "long", year: "numeric" })}
                    </span>
                  </td>
                  <td className="p-3 text-ink">Rp {inv.amount.toLocaleString("id-ID")}</td>
                  <td className="p-3">{statusBadge(inv.status)}</td>
                  <td className="p-3" onClick={(e) => e.stopPropagation()}>
                    <Button variant="secondary" size="md" onClick={() => loadDetail(inv.id)} className="px-3 text-xs">
                      Detail
                    </Button>
                  </td>
                </tr>
              ))}
              {filteredInvoices.length === 0 && (
                <tr>
                  <td colSpan={5} className="p-6 text-center text-sm text-ink-muted">
                    Tidak ada tagihan yang sesuai.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Right Detail Panel */}
      {(selectedId || loadingDetail) && (
        <div className="w-full lg:w-96 rounded-[var(--radius-lg)] border border-border bg-surface p-4 shadow-sm flex flex-col gap-4">
          <div className="flex items-center justify-between border-b border-border pb-2">
            <h2 className="font-semibold text-ink">Detail Tagihan</h2>
            <button
              onClick={() => {
                setSelectedId(null);
                setSelectedInvoice(null);
              }}
              className="text-ink-muted hover:text-ink text-xs font-medium"
            >
              Tutup
            </button>
          </div>

          {loadingDetail ? (
            <p className="text-sm text-ink-muted">Memuat detail...</p>
          ) : selectedInvoice ? (
            <div className="flex flex-col gap-3 text-sm">
              <div>
                <span className="text-xs text-ink-muted block">Nama Sekolah / Tenant</span>
                <span className="font-medium text-ink">{selectedInvoice.schoolName}</span>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <span className="text-xs text-ink-muted block">No. Tagihan</span>
                  <span className="font-mono text-xs font-semibold text-ink">{selectedInvoice.invoice.invoiceNumber}</span>
                </div>
                <div>
                  <span className="text-xs text-ink-muted block">Paket</span>
                  <span className="font-medium text-ink">{selectedInvoice.invoice.planName}</span>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <span className="text-xs text-ink-muted block">Total Tagihan</span>
                  <span className="font-semibold text-ink">Rp {selectedInvoice.invoice.amount.toLocaleString("id-ID")}</span>
                </div>
                <div>
                  <span className="text-xs text-ink-muted block">Kapasitas</span>
                  <span className="font-medium text-ink">{selectedInvoice.invoice.studentCapacity} Siswa</span>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <span className="text-xs text-ink-muted block">Tgl Penerbitan</span>
                  <span className="text-xs text-ink">{new Date(selectedInvoice.invoice.issuedAt).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" })}</span>
                </div>
                <div>
                  <span className="text-xs text-ink-muted block">Tgl Jatuh Tempo</span>
                  <span className="text-xs text-ink">{new Date(selectedInvoice.invoice.dueAt).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" })}</span>
                </div>
              </div>

              <div>
                <span className="text-xs text-ink-muted block">Status Saat Ini</span>
                <div className="mt-1">{statusBadge(selectedInvoice.invoice.status)}</div>
                {selectedInvoice.invoice.status === InvoiceStatus.Rejected && selectedInvoice.invoice.rejectionReason && (
                  <p className="mt-1 text-xs text-status-red">Alasan Penolakan: {selectedInvoice.invoice.rejectionReason}</p>
                )}
              </div>

              {/* Action Buttons for Pending Verification */}
              {selectedInvoice.invoice.status === InvoiceStatus.PendingVerification && (
                <div className="border-t border-border pt-3 flex flex-col gap-2">
                  <span className="text-xs font-semibold text-brand-action">Tinjau Pembayaran</span>

                  {selectedInvoice.invoice.proofKey && (
                    <Button
                      variant="secondary"
                      size="md"
                      loading={downloadingProof}
                      onClick={() => handleViewProof(selectedInvoice.invoice.id)}
                      className="w-full text-xs"
                    >
                      Lihat Bukti Transfer
                    </Button>
                  )}

                  {rejecting ? (
                    <div className="flex flex-col gap-2 rounded bg-surface-muted p-2 border border-border">
                      <Input
                        id="rejection-reason"
                        label="Alasan Penolakan (min 5 karakter)"
                        value={rejectionReason}
                        onChange={(e) => setRejectionReason(e.target.value)}
                        placeholder="Misal: Gambar bukti buram / tidak terbaca"
                        className="text-xs"
                      />
                      <div className="flex gap-2">
                        <Button
                          variant="primary"
                          size="md"
                          loading={actionBusy}
                          onClick={() => handleReject(selectedInvoice.invoice.id)}
                          className="flex-1 bg-status-red text-xs"
                        >
                          Kirim Tolak
                        </Button>
                        <Button
                          variant="secondary"
                          size="md"
                          onClick={() => {
                            setRejecting(false);
                            setRejectionReason("");
                          }}
                          className="px-2 text-xs"
                        >
                          Batal
                        </Button>
                      </div>
                    </div>
                  ) : (
                    <div className="flex gap-2">
                      <Button
                        variant="primary"
                        size="md"
                        loading={actionBusy}
                        onClick={() => handleConfirm(selectedInvoice.invoice.id)}
                        className="flex-1 text-xs"
                      >
                        Setujui & Aktifkan
                      </Button>
                      <Button
                        variant="secondary"
                        size="md"
                        onClick={() => setRejecting(true)}
                        className="px-3 text-xs text-status-red border-status-red hover:bg-status-red-bg"
                      >
                        Tolak
                      </Button>
                    </div>
                  )}
                </div>
              )}

              {/* Submissions History */}
              {selectedInvoice.submissions.length > 0 && (
                <div className="border-t border-border pt-3">
                  <span className="text-xs font-semibold text-ink block mb-2">Riwayat Pengajuan</span>
                  <div className="flex flex-col gap-2 max-h-48 overflow-y-auto">
                    {selectedInvoice.submissions.map((sub) => (
                      <div key={sub.id} className="text-xs bg-surface-muted p-2 rounded border border-border">
                        <div className="flex justify-between text-ink-muted">
                          <span>{new Date(sub.submittedAt).toLocaleDateString("id-ID", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}</span>
                          <span>
                            {sub.approved === true ? (
                              <span className="text-status-green font-medium">Disetujui</span>
                            ) : sub.approved === false ? (
                              <span className="text-status-red font-medium">Ditolak</span>
                            ) : (
                              <span className="text-ink-muted">Menunggu</span>
                            )}
                          </span>
                        </div>
                        {sub.note && <p className="mt-1 text-ink italic">&ldquo;Catatan: {sub.note}&rdquo;</p>}
                        {sub.verificationReason && <p className="mt-1 text-status-red font-medium">Alasan: {sub.verificationReason}</p>}
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-ink-muted">Pilih invoice dari tabel untuk melihat detail.</p>
          )}
        </div>
      )}
    </div>
  );
}
