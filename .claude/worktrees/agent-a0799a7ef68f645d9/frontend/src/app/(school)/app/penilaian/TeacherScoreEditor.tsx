"use client";

import { useEffect, useState } from "react";
import { EmptyState, ErrorState } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import { RubricAspectKind, type AssessmentDto } from "@/lib/apiTypes";
import { ScoreForm, type ScoreAspectInput } from "@/components/ScoreForm";

export interface TeacherScoreEditorProps {
  placementId: string;
}

/** Sisi sekolah/guru = SEMUA aspek yang BUKAN mentor-side (Softskill dkk) — cermin `!IsMentorSide` backend. */
function isMentorSide(kind: number): boolean {
  return kind === RubricAspectKind.Teknis || kind === RubricAspectKind.Kehadiran;
}

/**
 * VOK-H5-E2 §2 TeacherScoreEditor({placementId}) — sisi guru dari ScoreForm (Softskill dkk),
 * submit via SubmitTeacherScores. Sama struktur persis MentorScoreEditor.tsx, sengaja TIDAK
 * diekstrak jadi 1 komponen umum: jalur mentor (aspek Teknis+Kehadiran, endpoint mentor-scores)
 * vs guru (aspek lain, endpoint teacher-scores) beda cukup banyak (filter aspek+endpoint+lokasi
 * segment /mentor vs /app) - duplikasi kecil di sini lebih jelas dibaca drpd 1 komponen dgn banyak
 * prop kondisional.
 */
export function TeacherScoreEditor({ placementId }: TeacherScoreEditorProps) {
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

  const teacherAspects = assessment.aspects.filter((a) => !isMentorSide(a.kind));

  if (teacherAspects.length === 0) {
    return <EmptyState icon="📋" title="Belum ada rubrik" description="Admin sekolah belum membuat rubrik penilaian utk tenant ini." />;
  }

  const aspects: ScoreAspectInput[] = teacherAspects.map((a) => ({ id: a.aspectId, name: a.aspectName, kind: a.kind, weight: a.weight }));
  const values = Object.fromEntries(teacherAspects.map((a) => [a.aspectId, a.teacherValue]));

  return (
    <div className="flex flex-col gap-3">
      {assessment.isFinal && (
        <div className="rounded-[var(--radius-md)] border border-primary/30 bg-primary-muted p-3 text-sm text-ink">
          Penilaian sudah difinalisasi — nilai terkunci, skor final: <strong>{assessment.finalScore}</strong>
        </div>
      )}
      <ScoreForm
        aspects={aspects}
        values={values}
        readOnly={assessment.isFinal}
        onSave={async (aspectId, value) => {
          await apiClient.post(`/placements/${placementId}/assessment/teacher-scores`, [{ aspectId, value }]);
        }}
      />
    </div>
  );
}
