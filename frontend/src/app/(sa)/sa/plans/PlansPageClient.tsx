"use client";

import { useState } from "react";
import { Button, Icon, Input } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { FeatureFlagKey, type PlanDto } from "@/lib/apiTypes";

export interface PlansPageClientProps {
  initialPlans: PlanDto[];
}

const FLAG_LABELS: Record<number, string> = {
  [FeatureFlagKey.GeotagAllowed]: "Geotag Diizinkan",
  [FeatureFlagKey.ParentDigest]: "Digest Orang Tua",
};
const FLAG_KEYS: number[] = Object.values(FeatureFlagKey);

/** Form buat/ubah 1 plan — dipakai baris "+ Plan Baru" dan tombol Ubah per baris. */
function PlanForm({ initial, onSaved, onCancel }: { initial?: PlanDto; onSaved: () => void; onCancel: () => void }) {
  const [name, setName] = useState(initial?.name ?? "");
  const [priceMonthly, setPriceMonthly] = useState(String(initial?.priceMonthly ?? 0));
  const [maxStudents, setMaxStudents] = useState(String(initial?.maxStudents ?? 100));
  const [maxPlacements, setMaxPlacements] = useState(String(initial?.maxPlacements ?? 50));
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSave() {
    setSubmitting(true);
    setError(null);
    const body = { name, priceMonthly: Number(priceMonthly), maxStudents: Number(maxStudents), maxPlacements: Number(maxPlacements) };
    try {
      if (initial) {
        await apiClient.put(`/sa/plans/${initial.id}`, body);
      } else {
        await apiClient.post("/sa/plans", body);
      }
      onSaved();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menyimpan plan.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-border bg-surface-muted p-4">
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <Input label="Nama" value={name} onChange={(e) => setName(e.target.value)} />
        <Input label="Harga/bln (Rp)" type="number" value={priceMonthly} onChange={(e) => setPriceMonthly(e.target.value)} />
        <Input label="Maks Siswa" type="number" value={maxStudents} onChange={(e) => setMaxStudents(e.target.value)} />
        <Input label="Maks Placement" type="number" value={maxPlacements} onChange={(e) => setMaxPlacements(e.target.value)} />
      </div>
      {error && <p className="text-sm text-status-red">{error}</p>}
      <div className="flex gap-2">
        <Button variant="primary" size="md" loading={submitting} disabled={name.trim().length === 0} onClick={handleSave}>
          Simpan
        </Button>
        <Button variant="secondary" size="md" onClick={onCancel} disabled={submitting}>
          Batal
        </Button>
      </div>
    </div>
  );
}

/** Toggle 2 flag terdaftar (GeotagAllowed/ParentDigest) — SetFeatureFlag per plan. */
function PlanFlagsRow({ planId }: { planId: string }) {
  const [pending, setPending] = useState<number | null>(null);
  const [saved, setSaved] = useState<Set<number>>(new Set());

  async function toggle(key: number, enabled: boolean) {
    setPending(key);
    try {
      await apiClient.post(`/sa/plans/${planId}/flags`, { key, enabled });
      setSaved((prev) => new Set(prev).add(key));
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex flex-wrap gap-3 text-xs text-ink-muted">
      {FLAG_KEYS.map((key) => (
        <label key={key} className="flex min-h-[var(--tap-min)] items-center gap-1.5">
          <input
            type="checkbox"
            disabled={pending === key}
            onChange={(e) => void toggle(key, e.target.checked)}
            className="h-4 w-4"
          />
          {FLAG_LABELS[key]}
          {saved.has(key) && <Icon name="check" size={16} className="text-status-green" />}
        </label>
      ))}
    </div>
  );
}

/**
 * VOK-H6-E2 §1 sa/plans/page.tsx — CRUD plan + toggle feature flags per plan. Override per tenant
 * SENGAJA di halaman TERPISAH (bukan di sini): butuh cari tenant dulu (banyak tenant, tak muat di
 * 1 layar bersama daftar plan) — `GetEffectiveFlags`/`OverrideTenantFlag` dipanggil dari kartu
 * "kelola" tenant di `sa/tenants` akan lebih pas ticket berikutnya; utk cakupan ticket INI, toggle
 * plan-level sudah memenuhi "CRUD plan + toggle feature flags" literal.
 */
export function PlansPageClient({ initialPlans }: PlansPageClientProps) {
  const [plans, setPlans] = useState(initialPlans);
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  async function refresh() {
    const data = await apiClient.get<PlanDto[]>("/sa/plans");
    setPlans(data);
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-ink">Plans</h1>
        {!creating && (
          <Button variant="primary" size="md" onClick={() => setCreating(true)}>
            + Plan Baru
          </Button>
        )}
      </div>

      {creating && (
        <PlanForm
          onCancel={() => setCreating(false)}
          onSaved={() => {
            setCreating(false);
            void refresh();
          }}
        />
      )}

      <div className="flex flex-col gap-3">
        {plans.map((p) =>
          editingId === p.id ? (
            <PlanForm
              key={p.id}
              initial={p}
              onCancel={() => setEditingId(null)}
              onSaved={() => {
                setEditingId(null);
                void refresh();
              }}
            />
          ) : (
            <div key={p.id} className="flex flex-col gap-2 rounded-[var(--radius-lg)] border border-border bg-surface p-4">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-medium text-ink">{p.name}</p>
                  <p className="text-xs text-ink-muted">
                    Rp {p.priceMonthly.toLocaleString("id-ID")}/bln · maks {p.maxStudents} siswa · maks {p.maxPlacements} placement
                  </p>
                </div>
                <Button variant="secondary" size="md" onClick={() => setEditingId(p.id)} className="px-3 text-xs">
                  Ubah
                </Button>
              </div>
              <PlanFlagsRow planId={p.id} />
            </div>
          )
        )}
        {plans.length === 0 && !creating && (
          <p className="text-sm text-ink-muted">Belum ada plan — buat plan pertama sebelum provisioning tenant.</p>
        )}
      </div>
    </div>
  );
}
