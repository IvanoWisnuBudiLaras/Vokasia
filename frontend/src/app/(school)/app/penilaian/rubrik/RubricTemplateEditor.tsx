"use client";

import { useState } from "react";
import { ApiError, apiClient } from "@/lib/apiClient";
import { RubricAspectKind, type RubricDto } from "@/lib/apiTypes";

type AspectDraft = { name: string; kind: number; weight: number; description: string };

const kindLabels: Record<number, string> = {
  [RubricAspectKind.Teknis]: "Teknis / mentor",
  [RubricAspectKind.Softskill]: "Softskill / guru",
  [RubricAspectKind.Kehadiran]: "Kehadiran / mentor",
};

function toDraft(rubric: RubricDto | null): AspectDraft[] {
  return rubric?.aspects.map((aspect) => ({ name: aspect.name, kind: aspect.kind, weight: aspect.weight, description: aspect.description ?? "" })) ?? [
    { name: "Komunikasi", kind: RubricAspectKind.Softskill, weight: 20, description: "Menyampaikan informasi dengan jelas." },
    { name: "Kerja sama tim", kind: RubricAspectKind.Softskill, weight: 20, description: "Berkolaborasi dan memberi kontribusi." },
    { name: "Kepemimpinan", kind: RubricAspectKind.Softskill, weight: 15, description: "Mengambil inisiatif dan mengarahkan pekerjaan." },
    { name: "Teknis pekerjaan", kind: RubricAspectKind.Teknis, weight: 30, description: "Menguasai keterampilan kerja di DUDI." },
    { name: "Kehadiran", kind: RubricAspectKind.Kehadiran, weight: 15, description: "Hadir dan mengikuti aturan kerja." },
  ];
}

export function RubricTemplateEditor({ initialRubric, initialCompanyId, periodLabel }: { initialRubric: RubricDto | null; initialCompanyId: string | null; periodLabel: string }) {
  const [name, setName] = useState(initialRubric?.name ?? "Rubrik PKL Sekolah");
  const [aspects, setAspects] = useState<AspectDraft[]>(toDraft(initialRubric));
  const companyId = initialCompanyId ?? "";
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const totalWeight = aspects.reduce((sum, aspect) => sum + Number(aspect.weight || 0), 0);

  function updateAspect(index: number, field: keyof AspectDraft, value: string | number) {
    setAspects((current) => current.map((aspect, aspectIndex) => aspectIndex === index ? { ...aspect, [field]: value } : aspect));
  }

  async function save() {
    setBusy(true); setError(null); setMessage(null);
    const payload = { name, aspects: aspects.map((aspect) => ({ ...aspect, weight: Number(aspect.weight), description: aspect.description || null })), companyId: companyId || null };
    try {
      if (initialRubric) await apiClient.put(`/rubrics/${initialRubric.id}`, payload);
      else await apiClient.post("/rubrics", payload);
      setMessage("Rubrik aktif berhasil disimpan sebagai versi baru.");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Rubrik belum tersimpan.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-5">
      {message && <p role="status" className="border border-status-green/40 bg-status-green-bg p-3 text-sm text-ink">{message}</p>}
      {error && <p role="alert" className="border border-status-red/40 bg-status-red-bg p-3 text-sm text-status-red">{error}</p>}
      <div className="border-y border-border py-4"><p className="text-sm text-ink-muted">Periode yang ditinjau</p><p className="font-semibold text-ink">{periodLabel || "Periode aktif"}</p><p className="mt-1 text-xs text-ink-muted">Perubahan membuat versi baru agar assessment historis tetap memakai kriteria semula.</p></div>
      <label className="flex flex-col gap-1 text-sm font-medium text-ink">Cakupan DUDI<input value={companyId ? "Template khusus DUDI" : "Default seluruh sekolah"} readOnly className="h-11 border border-border bg-surface-muted px-3 font-normal" /></label>
      <label className="flex flex-col gap-1 text-sm font-medium text-ink">Nama rubrik<input value={name} onChange={(event) => setName(event.target.value)} className="h-11 border border-border bg-surface px-3 font-normal" /></label>
      <div className="overflow-x-auto border-y border-border"><table className="w-full min-w-[760px] text-left text-sm"><thead className="border-b border-border bg-surface-muted text-xs uppercase tracking-wide text-ink-muted"><tr><th className="px-3 py-3">Kriteria</th><th className="px-3 py-3">Pemilik nilai</th><th className="w-28 px-3 py-3">Bobot</th><th className="px-3 py-3">Deskripsi</th></tr></thead><tbody className="divide-y divide-border">{aspects.map((aspect, index) => <tr key={`${index}-${aspect.name}`}><td className="px-3 py-3"><input value={aspect.name} onChange={(event) => updateAspect(index, "name", event.target.value)} className="h-10 w-full border border-border px-2" /></td><td className="px-3 py-3"><select value={aspect.kind} onChange={(event) => updateAspect(index, "kind", Number(event.target.value))} className="h-10 w-full border border-border bg-surface px-2">{Object.entries(kindLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></td><td className="px-3 py-3"><input type="number" min="0" max="100" value={aspect.weight} onChange={(event) => updateAspect(index, "weight", Number(event.target.value))} className="h-10 w-full border border-border px-2" /></td><td className="px-3 py-3"><input value={aspect.description} onChange={(event) => updateAspect(index, "description", event.target.value)} className="h-10 w-full border border-border px-2" /></td></tr>)}</tbody></table></div>
      <div className="flex flex-wrap items-center justify-between gap-3"><p className={`text-sm font-semibold ${totalWeight === 100 ? "text-status-green" : "text-status-red"}`}>Total bobot: {totalWeight}% {totalWeight === 100 ? "(valid)" : "(harus 100%)"}</p><button type="button" onClick={() => setAspects((current) => [...current, { name: "Kriteria baru", kind: RubricAspectKind.Softskill, weight: 0, description: "" }])} className="min-h-11 border border-border px-3 text-sm font-semibold text-primary">Tambah kriteria</button><button type="button" onClick={() => void save()} disabled={busy || totalWeight !== 100 || aspects.length === 0} className="min-h-11 bg-primary px-4 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">{busy ? "Menyimpan..." : "Simpan versi rubrik"}</button></div>
    </div>
  );
}
