"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { PlanDto, TenantDto } from "@/lib/apiTypes";

export interface EditTenantPanelProps {
  tenant: TenantDto;
  plans: PlanDto[];
  onClose: () => void;
  onSaved: () => void;
}

export function EditTenantPanel({ tenant, plans, onClose, onSaved }: EditTenantPanelProps) {
  const [schoolName, setSchoolName] = useState(tenant.schoolName);
  const [npsn, setNpsn] = useState(tenant.npsn ?? "");
  const [city, setCity] = useState(tenant.city ?? "");
  const [address, setAddress] = useState(tenant.address ?? "");
  const [planId, setPlanId] = useState(tenant.planId ?? plans[0]?.id ?? "");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!schoolName.trim()) {
      setError("Nama sekolah wajib diisi.");
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      await apiClient.put(`/sa/tenants/${tenant.id}`, {
        schoolName: schoolName.trim(),
        npsn: npsn.trim() || null,
        city: city.trim() || null,
        address: address.trim() || null,
        planId: planId || null,
      });
      onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal mengakhiri pembaruan tenant.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="rounded-[var(--radius-lg)] border border-border bg-surface p-4 shadow-sm">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-base font-semibold text-ink">Edit Data Tenant — {tenant.schoolName}</h2>
        <Button variant="secondary" size="md" onClick={onClose} className="px-2 text-xs">
          Batal
        </Button>
      </div>

      {error && <p className="mb-3 text-xs text-status-red">{error}</p>}

      <form onSubmit={handleSubmit} className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label className="block text-xs font-medium text-ink-muted">Nama Sekolah *</label>
          <input
            type="text"
            required
            value={schoolName}
            onChange={(e) => setSchoolName(e.target.value)}
            className="mt-1 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-ink-muted">NPSN</label>
          <input
            type="text"
            value={npsn}
            onChange={(e) => setNpsn(e.target.value)}
            className="mt-1 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-ink-muted">Kota / Kab</label>
          <input
            type="text"
            value={city}
            onChange={(e) => setCity(e.target.value)}
            className="mt-1 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"
          />
        </div>

        <div>
          <label className="block text-xs font-medium text-ink-muted">Paket Plan</label>
          <select
            value={planId}
            onChange={(e) => setPlanId(e.target.value)}
            className="mt-1 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"
          >
            {plans.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </div>

        <div className="sm:col-span-2">
          <label className="block text-xs font-medium text-ink-muted">Alamat Sekolah</label>
          <input
            type="text"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            className="mt-1 h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus"
          />
        </div>

        <div className="flex justify-end gap-2 pt-2 sm:col-span-2">
          <Button type="button" variant="secondary" size="md" onClick={onClose} disabled={submitting}>
            Batal
          </Button>
          <Button type="submit" variant="primary" size="md" loading={submitting}>
            Simpan Perubahan
          </Button>
        </div>
      </form>
    </div>
  );
}
