"use client";

import { useMemo, useState } from "react";
import { Button, Pagination, StatusBadge, TableExportToolbar, Tooltip } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { Paged, PlanDto, TenantDto } from "@/lib/apiTypes";
import { ImpersonatePanel } from "./ImpersonatePanel";
import { TenantWizard } from "./TenantWizard";
import { EditTenantPanel } from "./EditTenantPanel";

export interface TenantsTableProps {
  initialTenants: TenantDto[];
  plans: PlanDto[];
}

/** Panel konfirmasi nonaktifkan tenant — pola sama FinalizeButton (2 langkah, alasan wajib diisi). */
function DeactivateAction({ tenant, onDone }: { tenant: TenantDto; onDone: () => void }) {
  const [confirming, setConfirming] = useState(false);
  const [reason, setReason] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleConfirm() {
    setSubmitting(true);
    setError(null);
    try {
      await apiClient.post(`/sa/tenants/${tenant.id}/deactivate`, { reason });
      onDone();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menonaktifkan tenant.");
    } finally {
      setSubmitting(false);
    }
  }

  if (!tenant.isActive) {
    return <span className="text-xs text-ink-muted">Nonaktif</span>;
  }

  if (confirming) {
    return (
      <div className="flex flex-col items-end gap-1.5">
        <input
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          aria-label={`Alasan menonaktifkan ${tenant.schoolName}`}
          placeholder="Alasan nonaktif…"
          className="h-[var(--tap-min)] w-48 rounded-[var(--radius-md)] border border-border bg-surface px-2 text-xs text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1"
        />
        {error && <span className="text-xs text-status-red">{error}</span>}
        <div className="flex gap-1.5">
          <Button variant="danger" size="md" loading={submitting} disabled={reason.trim().length === 0} onClick={handleConfirm} className="px-3 text-xs">
            Ya, Nonaktifkan
          </Button>
          <Button variant="secondary" size="md" onClick={() => setConfirming(false)} disabled={submitting} className="px-3 text-xs">
            Batal
          </Button>
        </div>
      </div>
    );
  }

  return (
    <Button variant="danger-outline" size="md" onClick={() => setConfirming(true)} className="px-3 text-xs">
      Nonaktifkan
    </Button>
  );
}

/**
 * VOK-H6-E2 §1 sa/tenants/page.tsx — tabel tenant (cari, plan, status) + ⋮ kelola (nonaktifkan) +
 * tombol "+ Tenant Baru" membuka TenantWizard inline (pola sama FinalizeButton: panel inline, bukan
 * modal — tak ada komponen Dialog/Modal bersama di codebase ini, dicek grep).
 */
export function TenantsTable({ initialTenants, plans }: TenantsTableProps) {
  const [tenants, setTenants] = useState(initialTenants);
  const [query, setQuery] = useState("");
  const [planFilter, setPlanFilter] = useState<string>("");
  const [activeFilter, setActiveFilter] = useState<string>("");
  const [wizardOpen, setWizardOpen] = useState(false);
  const [creationStatus, setCreationStatus] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [impersonatingTenantId, setImpersonatingTenantId] = useState<string | null>(null);
  const [editingTenantId, setEditingTenantId] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const planNameById = useMemo(() => new Map(plans.map((p) => [p.id, p.name])), [plans]);

  async function refresh() {
    setRefreshing(true);
    try {
      const data = await apiClient.get<Paged<TenantDto>>("/sa/tenants?pageSize=100");
      setTenants(data.items);
    } finally {
      setRefreshing(false);
    }
  }

  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    return tenants.filter((t) => {
      if (q.length > 0 && !t.schoolName.toLowerCase().includes(q) && !(t.npsn ?? "").includes(q)) return false;
      if (planFilter.length > 0 && t.planId !== planFilter) return false;
      if (activeFilter === "active" && !t.isActive) return false;
      if (activeFilter === "inactive" && t.isActive) return false;
      return true;
    });
  }, [tenants, query, planFilter, activeFilter]);

  const paginatedTenants = useMemo(() => {
    return visible.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  }, [visible, currentPage, pageSize]);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold text-ink">Tenants</h1>
        {!wizardOpen && (
          <Button
            variant="primary"
            size="md"
            onClick={() => {
              setCreationStatus(null);
              setWizardOpen(true);
            }}
          >
            + Tenant Baru
          </Button>
        )}
      </div>

      {wizardOpen && (
        <TenantWizard
          plans={plans}
          onCancel={() => setWizardOpen(false)}
          onCreated={() => {
            setWizardOpen(false);
            setCreationStatus("Undangan admin sudah dikirim. Admin akan mengatur kata sandi melalui tautan satu kali.");
            void refresh();
          }}
        />
      )}

      {creationStatus && (
        <div role="status" className="border-l-4 border-status-green bg-status-green-bg p-4 text-sm text-ink">
          <strong className="block">Tenant berhasil dibuat.</strong>
          <span>{creationStatus}</span>
        </div>
      )}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-2">
          <input
            type="search"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setCurrentPage(1);
            }}
            aria-label="Cari sekolah berdasarkan nama atau NPSN"
            placeholder="Cari nama sekolah / NPSN…"
            className="h-[var(--tap-min)] w-64 rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
          />
          <select
            value={planFilter}
            onChange={(e) => {
              setPlanFilter(e.target.value);
              setCurrentPage(1);
            }}
            aria-label="Filter sekolah berdasarkan paket"
            className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1"
          >
            <option value="">Semua paket</option>
            {plans.map((p) => (
              <option key={p.id} value={p.id}>{p.name}</option>
            ))}
          </select>
          <select
            value={activeFilter}
            onChange={(e) => {
              setActiveFilter(e.target.value);
              setCurrentPage(1);
            }}
            aria-label="Filter sekolah berdasarkan status"
            className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1"
          >
            <option value="">Semua status</option>
            <option value="active">Aktif</option>
            <option value="inactive">Nonaktif</option>
          </select>
        </div>

        <TableExportToolbar
          data={visible}
          filename="daftar_tenant_smk"
          title="Daftar Tenant SMK — Vokasia Platform"
          columns={[
            { key: "schoolName", label: "Nama Sekolah" },
            { key: "npsn", label: "NPSN" },
            { key: "city", label: "Kota" },
            { key: "planId", label: "Paket Plan", format: (val) => planNameById.get(val) ?? "—" },
            { key: "isActive", label: "Status", format: (val) => (val ? "Aktif" : "Nonaktif") },
          ]}
        />
      </div>

      {refreshing && <p className="text-xs text-ink-muted">Memuat ulang…</p>}

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border bg-surface shadow-sm">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted border-b border-border font-medium text-ink-muted">
            <tr>
              <th className="p-3">Sekolah</th>
              <th className="p-3">Kota</th>
              <th className="p-3">Plan</th>
              <th className="p-3">Status</th>
              <th className="p-3">Kelola</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border">
            {paginatedTenants.map((t) => (
              <tr key={t.id} className="hover:bg-surface-muted/50">
                <td className="p-3 font-medium text-ink">{t.schoolName}{t.npsn && <span className="ml-1 text-xs text-ink-muted">({t.npsn})</span>}</td>
                <td className="p-3 text-ink-muted">{t.city ?? "—"}</td>
                <td className="p-3 text-ink-muted">{t.planId ? planNameById.get(t.planId) ?? "—" : "—"}</td>
                <td className="p-3">
                  {t.isActive ? <StatusBadge status="green" label="Aktif" /> : <StatusBadge status="red" label="Nonaktif" />}
                </td>
                <td className="p-3">
                  <div className="flex flex-col items-end gap-1.5">
                    <div className="flex flex-wrap justify-end gap-1.5">
                      <Tooltip content="Ubah profil sekolah, NPSN, kota, alamat, atau paket plan tenant">
                        <Button
                          variant="secondary"
                          size="md"
                          onClick={() => setEditingTenantId(editingTenantId === t.id ? null : t.id)}
                          className="px-3 text-xs"
                        >
                          Edit
                        </Button>
                      </Tooltip>
                      <Tooltip content="Membekukan akses sekolah & siswa tenant ini sementara tanpa menghapus data">
                        <DeactivateAction tenant={t} onDone={refresh} />
                      </Tooltip>
                      {t.isActive && impersonatingTenantId !== t.id && (
                        <Tooltip content="Masuk sementara sebagai Admin Sekolah ini untuk verifikasi tampilan / bantuan teknis">
                          <Button variant="secondary" size="md" onClick={() => setImpersonatingTenantId(t.id)} className="px-3 text-xs">
                            Impersonasi
                          </Button>
                        </Tooltip>
                      )}
                    </div>
                    {editingTenantId === t.id && (
                      <div className="mt-2 w-full text-left">
                        <EditTenantPanel
                          tenant={t}
                          plans={plans}
                          onClose={() => setEditingTenantId(null)}
                          onSaved={() => {
                            setEditingTenantId(null);
                            void refresh();
                          }}
                        />
                      </div>
                    )}
                    {impersonatingTenantId === t.id && (
                      <ImpersonatePanel tenantId={t.id} onClose={() => setImpersonatingTenantId(null)} />
                    )}
                  </div>
                </td>
              </tr>
            ))}
            {paginatedTenants.length === 0 && (
              <tr>
                <td colSpan={5} className="p-6 text-center text-sm text-ink-muted">Tidak ada tenant yang cocok.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <Pagination
        currentPage={currentPage}
        totalItems={visible.length}
        pageSize={pageSize}
        onPageChange={(page) => setCurrentPage(page)}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setCurrentPage(1);
        }}
      />
    </div>
  );
}
