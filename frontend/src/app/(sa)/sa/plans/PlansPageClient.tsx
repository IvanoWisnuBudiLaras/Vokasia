"use client";

import { useState } from "react";
import { Button, Icon, Input, TableExportToolbar, Tooltip } from "@/components/ui";
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
        <label key={key} className="flex min-h-[var(--tap-min)] items-center gap-1.5 cursor-pointer">
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

export function PlansPageClient({ initialPlans }: PlansPageClientProps) {
  const [plans, setPlans] = useState(initialPlans);
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    const data = await apiClient.get<PlanDto[]>("/sa/plans");
    setPlans(data);
  }

  async function handleDelete(id: string) {
    setDeletingId(id);
    setError(null);
    try {
      await apiClient.delete(`/sa/plans/${id}`);
      await refresh();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menghapus plan.");
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold text-ink">Paket Langganan (Plans)</h1>
        <div className="flex items-center gap-2">
          <TableExportToolbar
            data={plans}
            filename="daftar_paket_plan_vokasia"
            title="Daftar Paket Langganan Vokasia Platform"
            columns={[
              { key: "name", label: "Nama Paket" },
              { key: "priceMonthly", label: "Harga/Bulan", format: (val) => `Rp ${val.toLocaleString("id-ID")}` },
              { key: "maxStudents", label: "Maks Siswa" },
              { key: "maxPlacements", label: "Maks Placement" },
            ]}
          />
          {!creating && (
            <Button variant="primary" size="md" onClick={() => setCreating(true)}>
              + Plan Baru
            </Button>
          )}
        </div>
      </div>

      {error && <p className="text-sm text-status-red">{error}</p>}

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
            <div key={p.id} className="flex flex-col gap-2 rounded-[var(--radius-lg)] border border-border bg-surface p-4 shadow-sm">
              <div className="flex items-center justify-between">
                <div>
                  <p className="font-semibold text-ink">{p.name}</p>
                  <p className="text-xs text-ink-muted">
                    Rp {p.priceMonthly.toLocaleString("id-ID")}/bln · maks {p.maxStudents} siswa · maks {p.maxPlacements} placement
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <Tooltip content="Ubah nama, harga, atau kuota batas siswa/placement paket ini">
                    <Button variant="secondary" size="md" onClick={() => setEditingId(p.id)} className="px-3 text-xs">
                      Ubah
                    </Button>
                  </Tooltip>
                  <Tooltip content="Hapus paket langganan ini jika tidak ada tenant yang sedang menggunakannya">
                    <Button
                      variant="danger-outline"
                      size="md"
                      loading={deletingId === p.id}
                      onClick={() => {
                        if (confirm(`Apakah Anda yakin ingin menghapus paket "${p.name}"?`)) {
                          void handleDelete(p.id);
                        }
                      }}
                      className="px-3 text-xs"
                    >
                      Hapus
                    </Button>
                  </Tooltip>
                </div>
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
