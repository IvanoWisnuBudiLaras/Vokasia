"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { CompanySearchDto, MergeResultDto } from "@/lib/apiTypes";

export interface MergeCompanyDialogProps {
  sourceId: string;
  sourceName: string;
  onMerged: () => void;
  onCancel: () => void;
}

/**
 * VOK-H6-E2 §1 MergeCompanyDialog({sourceId}) — cari target via SearchCompanies (autocomplete),
 * lalu merge. [GAP dicatat, bukan diam-diam]: ticket minta "preview dampak (jumlah placement
 * pindah)" SEBELUM merge — backend `MergeCompanies` (H6-E1) TIDAK punya endpoint dry-run/preview
 * apa pun (dikonfirmasi baca Vokasia.Api/Endpoints/SaCompaniesEndpoints.cs), dan menambah satu di
 * sini di luar wilayah ticket ini (`frontend/` saja, "mengubah kontrak OpenAPI" dilarang eksplisit
 * PROMPT TEMPLATE). Diganti dgn peringatan tegas pra-eksekusi (bukan angka pra-estimasi yang bisa
 * salah) + angka NYATA (movedTenantCompanies/movedPlacements) ditampilkan SETELAH merge sukses —
 * pola sama FinalizeButton (H5-E2): tampilkan hasil sungguhan, bukan pra-estimasi.
 */
export function MergeCompanyDialog({ sourceId, sourceName, onMerged, onCancel }: MergeCompanyDialogProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<CompanySearchDto[]>([]);
  const [searching, setSearching] = useState(false);
  const [target, setTarget] = useState<CompanySearchDto | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<MergeResultDto | null>(null);

  async function handleSearch(q: string) {
    setQuery(q);
    setTarget(null);
    if (q.trim().length < 2) {
      setResults([]);
      return;
    }
    setSearching(true);
    try {
      const data = await apiClient.get<CompanySearchDto[]>(`/sa/companies/search?q=${encodeURIComponent(q)}&limit=8`);
      setResults(data.filter((c) => c.id !== sourceId));
    } finally {
      setSearching(false);
    }
  }

  async function handleMerge() {
    if (!target) return;
    setSubmitting(true);
    setError(null);
    try {
      const res = await apiClient.post<MergeResultDto>("/sa/companies/merge", { sourceId, targetId: target.id });
      setResult(res);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menggabungkan company.");
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-status-green/30 bg-status-green-bg p-3 text-sm text-status-green">
        <p>
          &quot;{sourceName}&quot; digabung ke target. {result.movedTenantCompanies} link tenant dipindah,{" "}
          {result.movedPlacements} placement dipindah. Riwayat merge tercatat.
        </p>
        <Button variant="secondary" size="md" onClick={onMerged} className="self-end">
          Tutup
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-border bg-surface-muted p-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-ink">Gabungkan &quot;{sourceName}&quot; ke…</h3>
        <Button variant="secondary" size="md" onClick={onCancel} disabled={submitting}>Batal</Button>
      </div>

      <input
        value={query}
        onChange={(e) => void handleSearch(e.target.value)}
        aria-label="Cari DUDI tujuan penggabungan"
        placeholder="Cari company target…"
        className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1"
      />

      {searching && <p className="text-xs text-ink-muted">Mencari…</p>}

      {results.length > 0 && !target && (
        <ul className="flex flex-col gap-1">
          {results.map((c) => (
            <li key={c.id}>
              <button
                type="button"
                onClick={() => setTarget(c)}
                className="min-h-[var(--tap-min)] w-full rounded-[var(--radius-sm)] px-2 py-2 text-left text-sm text-ink outline-none transition-[color,background-color,border-color] hover:bg-surface focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
              >
                {c.name} {c.city && <span className="text-ink-muted">— {c.city}</span>}
              </button>
            </li>
          ))}
        </ul>
      )}

      {target && !confirming && (
        <div className="flex items-center justify-between rounded-[var(--radius-md)] border border-border bg-surface p-2 text-sm">
          <span>Target: <strong>{target.name}</strong></span>
          <Button variant="primary" size="md" onClick={() => setConfirming(true)} className="px-3 text-xs">
            Lanjut
          </Button>
        </div>
      )}

      {target && confirming && (
        <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-status-amber/40 bg-status-amber-bg p-3">
          <p className="text-sm text-ink">
            Seluruh link tenant &amp; placement dari &quot;{sourceName}&quot; akan dipindah ke &quot;{target.name}&quot;.
            &quot;{sourceName}&quot; ditandai merged (tidak dihapus, riwayat tersimpan) — tindakan ini TIDAK bisa dibatalkan.
          </p>
          {error && <p className="text-sm text-status-red">{error}</p>}
          <div className="flex gap-2">
            <Button variant="danger" size="md" loading={submitting} onClick={handleMerge}>
              Ya, Gabungkan
            </Button>
            <Button variant="secondary" size="md" disabled={submitting} onClick={() => setConfirming(false)}>
              Batal
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
