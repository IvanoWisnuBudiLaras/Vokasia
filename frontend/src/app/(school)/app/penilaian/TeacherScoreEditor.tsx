"use client";

import { useEffect, useState } from "react";
import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import { RubricAspectKind, type AssessmentDto } from "@/lib/apiTypes";
import { ScoreForm, type ScoreAspectInput } from "@/components/ScoreForm";

export interface TeacherScoreEditorProps {
  placementId: string;
}

function isMentorSide(kind: number): boolean {
  return kind === RubricAspectKind.Teknis || kind === RubricAspectKind.Kehadiran;
}

export function TeacherScoreEditor({ placementId }: TeacherScoreEditorProps) {
  const [assessment, setAssessment] = useState<AssessmentDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  async function load() {
    setLoading(true);
    setError(false);
    try {
      setAssessment(await apiClient.get<AssessmentDto>(`/placements/${placementId}/assessment`));
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    // Loading is an external side effect; it updates the view when the request resolves.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [placementId]);

  if (loading) return <p className="text-sm text-ink-muted">Memuat rubrik penilaian…</p>;
  if (error || !assessment) return <ErrorState message="Rubrik penilaian belum bisa dimuat." onRetry={load} />;

  const teacherAspects = assessment.aspects.filter((aspect) => !isMentorSide(aspect.kind));
  if (teacherAspects.length === 0) {
    return <EmptyState icon={<Icon name="clipboard-check" size={32} />} title="Belum ada rubrik" description="Admin sekolah belum membuat rubrik penilaian untuk sekolah ini." />;
  }

  const aspects: ScoreAspectInput[] = teacherAspects.map((aspect) => ({
    id: aspect.aspectId,
    name: aspect.aspectName,
    kind: aspect.kind,
    weight: aspect.weight,
    description: aspect.description,
  }));
  const values = Object.fromEntries(teacherAspects.map((aspect) => [aspect.aspectId, aspect.teacherValue]));
  const comments = Object.fromEntries(teacherAspects.map((aspect) => [aspect.aspectId, aspect.teacherComment]));

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
        comments={comments}
        readOnly={assessment.isFinal}
        onSave={async (aspectId, value, comment) => {
          await apiClient.post(`/placements/${placementId}/assessment/teacher-scores`, [{ aspectId, value, comment }]);
        }}
      />
    </div>
  );
}
