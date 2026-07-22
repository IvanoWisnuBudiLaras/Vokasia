"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { FinalizeAssessmentResult, RecapRowDto } from "@/lib/apiTypes";

export interface FinalizeButtonProps {
  periodId: string;
  incompleteCount: number;
  rows: RecapRowDto[];
  onFinalized: () => void;
}

/**
 * VOK-H5-E2 §3 FinalizeButton({periodId, incompleteCount, rows, onFinalized}) — konfirmasi DUA
 * LANGKAH literal (klik "Finalisasi" -> panel konfirmasi tampil terpisah, klik lagi "Ya,
 * Finalisasi" utk eksekusi sungguhan) + peringatan "X siswa belum lengkap" bila `incompleteCount`
 * (dihitung caller dari `rows` yang statusnya != "Final") > 0.
 *
 * Backend `FinalizeAssessment` mode BATCH (PlacementId=null, VOK-H5-E1 D33) SELALU 200 (bukan
 * 422) — yang lengkap difinalisasi, yang kurang dilaporkan di body yang sama. "422-aware" (AC)
 * di sini berarti: hasil NYATA dari `result.incomplete` (bukan `incompleteCount` prop yang cuma
 * estimasi awal dari status rekap) yang ditampilkan sbg ringkasan akhir — lebih akurat drpd
 * pre-estimate krn dihitung backend dari kelengkapan skor SEBENARNYA, bukan status "Final"/tidak.
 */
export function FinalizeButton({ periodId, incompleteCount, rows, onFinalized }: FinalizeButtonProps) {
  const [confirming, setConfirming] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [summary, setSummary] = useState<string | null>(null);

  async function handleConfirm() {
    setSubmitting(true);
    setError(null);
    try {
      const result = await apiClient.post<FinalizeAssessmentResult>(`/periods/${periodId}/assessments/finalize`, {
        periodId,
        placementId: null,
      });

      const nameByPlacement = new Map(rows.map((r) => [r.placementId, r.studentName]));
      const incompleteNames = result.incomplete.map((i) => nameByPlacement.get(i.placementId) ?? i.placementId).join(", ");
      setSummary(
        result.incomplete.length === 0
          ? `${result.finalized.length} siswa difinalisasi. Semua lengkap.`
          : `${result.finalized.length} siswa difinalisasi. ${result.incomplete.length} siswa masih kurang lengkap (tetap draft): ${incompleteNames}.`
      );
      setConfirming(false);
      onFinalized();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal memfinalisasi periode. Coba lagi.");
    } finally {
      setSubmitting(false);
    }
  }

  if (summary) {
    return (
      <div className="rounded-[var(--radius-md)] border border-status-green/30 bg-status-green-bg p-3 text-sm text-status-green">
        {summary}
      </div>
    );
  }

  if (confirming) {
    return (
      <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-status-amber/40 bg-status-amber-bg p-3">
        <p className="text-sm text-ink">
          {incompleteCount > 0
            ? `${incompleteCount} siswa belum lengkap skornya. Yang lengkap akan langsung final & terkunci; sisanya tetap draft. Lanjutkan?`
            : "Finalisasi periode ini? Nilai yang sudah final tidak bisa diubah lagi."}
        </p>
        {error && <p className="text-sm text-status-red">{error}</p>}
        <div className="flex gap-2">
          <Button variant="danger" size="md" loading={submitting} onClick={handleConfirm}>
            Ya, Finalisasi
          </Button>
          <Button variant="secondary" size="md" disabled={submitting} onClick={() => setConfirming(false)}>
            Batal
          </Button>
        </div>
      </div>
    );
  }

  return (
    <Button variant="primary" size="md" onClick={() => setConfirming(true)}>
      Finalisasi Periode
    </Button>
  );
}
