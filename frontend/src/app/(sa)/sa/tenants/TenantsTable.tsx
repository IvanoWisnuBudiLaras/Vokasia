"use client";

import { useMemo, useState } from "react";
import { Button, StatusBadge } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { Paged, PlanDto, TenantDto } from "@/lib/apiTypes";
import { TenantWizard } from "./TenantWizard";

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
          placeholder="Alasan nonaktif…"
          className="h-8 w-48 rounded-[var(--radius-md)] border border-border bg-surface px-2 text-xs text-ink outline-none"
        />
        {error && <span className="text-xs text-status-red">{error}</span>}
        <div className="flex gap-1.5">
          <Button variant="danger" size="md" loading={submitting} disabled={reason.trim().length === 0} onClick={handleConfirm} className="h-8 px-3 text-xs">
            Ya, Nonaktifkan
          </Button>
          <Button variant="secondary" size="md" onClick={() => setConfirming(false)} disabled={submitting} className="h-8 px-3 text-xs">
            Batal
          </Button>
        </div>
      </div>
    );
  }

  return (
    <Button variant="secondary" size="md" onClick={() => setConfirming(true)} className="h-8 px-3 text-xs">
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
  const [refreshing, setRefreshing] = useState(false);

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

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold text-ink">Tenants</h1>
        {!wizardOpen && (
          <Button variant="primary" size="md" onClick={() => setWizardOpen(true)}>
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
            void refresh();
          }}
        />
      )}

      <div className="flex flex-wrap gap-2">
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Cari nama sekolah / NPSN…"
          className="h-9 w-64 rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
        />
        <select
          value={planFilter}
          onChange={(e) => setPlanFilter(e.target.value)}
          className="h-9 rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm text-ink"
        >
          <option value="">Semua Plan</option>
          {plans.map((p) => (
            <option key={p.id} value={p.id}>{p.name}</option>
          ))}
        </select>
        <select
          value={activeFilter}
          onChange={(e) => setActiveFilter(e.target.value)}
          className="h-9 rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm text-ink"
        >
          <option value="">Semua Status</option>
          <option value="active">Aktif</option>
          <option value="inactive">Nonaktif</option>
        </select>
      </div>

      {refreshing && <p className="text-xs text-ink-muted">Memuat ulang…</p>}

      <div className="overflow-x-auto rounded-[var(--radius-lg)] border border-border">
        <table className="w-full text-left text-sm">
          <thead className="bg-surface-muted">
            <tr>
              <th className="p-3 font-medium text-ink">Sekolah</th>
              <th className="p-3 font-medium text-ink">Kota</th>
              <th className="p-3 font-medium text-ink">Plan</th>
              <th className="p-3 font-medium text-ink">Status</th>
              <th className="p-3 font-medium text-ink">Kelola</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((t) => (
              <tr key={t.id} className="border-t border-border">
                <td className="p-3 font-medium text-ink">{t.schoolName}{t.npsn && <span className="ml-1 text-xs text-ink-muted">({t.npsn})</span>}</td>
                <td className="p-3 text-ink-muted">{t.city ?? "—"}</td>
                <td className="p-3 text-ink-muted">{t.planId ? planNameById.get(t.planId) ?? "—" : "—"}</td>
                <td className="p-3">
                  {t.isActive ? <StatusBadge status="green" label="Aktif" /> : <StatusBadge status="red" label="Nonaktif" />}
                </td>
                <td className="p-3"><DeactivateAction tenant={t} onDone={refresh} /></td>
              </tr>
            ))}
            {visible.length === 0 && (
              <tr>
                <td colSpan={5} className="p-6 text-center text-sm text-ink-muted">Tidak ada tenant yang cocok.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
