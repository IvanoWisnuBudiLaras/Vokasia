"use client";

import { useEffect, useState } from "react";
import { EmptyState, ErrorState } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import { RubricAspectKind, type AssessmentDto } from "@/lib/apiTypes";
import { ScoreForm, type ScoreAspectInput } from "@/components/ScoreForm";

export interface MentorScoreEditorProps {
  placementId: string;
}

/** Sisi DUDI/mentor (Teknis+Kehadiran) — cermin persis `AssessmentEndpoints.IsMentorSide` backend. */
function isMentorSide(kind: number): boolean {
  return kind === RubricAspectKind.Teknis || kind === RubricAspectKind.Kehadiran;
}

/**
 * VOK-H5-E2 §2 MentorScoreEditor({placementId}) — bungkus client ScoreForm khusus sisi mentor:
 * fetch GetAssessment, filter aspek Teknis+Kehadiran, submit via SubmitMentorScores per-aspek
 * (autosave, lihat doc-comment ScoreForm.tsx). readOnly otomatis kalau assessment.isFinal (AC:
 * "admin finalize sukses -> semua ScoreForm jadi readOnly").
 */
export function MentorScoreEditor({ placementId }: MentorScoreEditorProps) {
  const [assessment, setAssessment] = useState<AssessmentDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  async function load() {
    setLoading(true);
    setError(false);
    try {
      const data = await apiClient.get<AssessmentDto>(`/placements/${placementId}/assessment`);
      setAssessment(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [placementId]);

  if (loading) {
    return <p className="text-sm text-ink-muted">Memuat rubrik penilaian…</p>;
  }

  if (error || !assessment) {
    return <ErrorState message="Rubrik penilaian belum bisa dimuat." onRetry={load} />;
  }

  const mentorAspects = assessment.aspects.filter((a) => isMentorSide(a.kind));

  if (mentorAspects.length === 0) {
    return <EmptyState icon="📋" title="Belum ada rubrik" description="Admin sekolah belum membuat rubrik penilaian utk tenant ini." />;
  }

  const aspects: ScoreAspectInput[] = mentorAspects.map((a) => ({ id: a.aspectId, name: a.aspectName, kind: a.kind, weight: a.weight }));
  const values = Object.fromEntries(mentorAspects.map((a) => [a.aspectId, a.mentorValue]));

  return (
    <div className="flex flex-col gap-3">
      {assessment.isFinal && (
        <div className="rounded-[var(--radius-md)] border border-primary/30 bg-primary-muted p-3 text-sm text-ink">
          Penilaian sudah difinalisasi oleh admin — nilai terkunci, skor final: <strong>{assessment.finalScore}</strong>
        </div>
      )}
      <ScoreForm
        aspects={aspects}
        values={values}
        readOnly={assessment.isFinal}
        onSave={async (aspectId, value) => {
          await apiClient.post(`/placements/${placementId}/assessment/mentor-scores`, [{ aspectId, value }]);
        }}
      />
    </div>
  );
}
