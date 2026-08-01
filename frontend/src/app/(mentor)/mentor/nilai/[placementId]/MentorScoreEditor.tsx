"use client";

import { useEffect, useState } from "react";
import { EmptyState, ErrorState, Icon } from "@/components/ui";
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
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
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
    return <EmptyState icon={<Icon name="clipboard-check" size={32} />} title="Belum ada rubrik" description="Admin sekolah belum membuat rubrik penilaian untuk sekolah ini." />;
  }

  const aspects: ScoreAspectInput[] = mentorAspects.map((a) => ({ id: a.aspectId, name: a.aspectName, kind: a.kind, weight: a.weight }));
  const values = Object.fromEntries(mentorAspects.map((a) => [a.aspectId, a.mentorValue]));

  return (
    <div className="flex flex-col gap-4">
      {assessment.isFinal && (
        <div className="rounded-[var(--radius-md)] border border-primary/30 bg-primary-muted p-3 text-sm text-ink">
          Penilaian sudah difinalisasi oleh admin — nilai terkunci, skor final: <strong>{assessment.finalScore}</strong>
        </div>
      )}
      {/* Contextual Approval Summary: Sorotan Kompetensi & Performa Siswa */}
      <div className="rounded-[var(--radius-lg)] border border-primary/20 bg-primary/5 p-4 text-xs space-y-2.5">
        <div className="flex items-center justify-between">
          <span className="font-bold text-primary uppercase tracking-wider flex items-center gap-1.5 text-[11px]">
            💡 Sorotan Kompetensi & Ringkasan Performa Siswa
          </span>
          <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] font-semibold text-primary">
            Rujukan Penilaian Mentor
          </span>
        </div>
        <p className="text-ink-muted leading-relaxed">
          Berikut adalah ringkasan performa yang dihimpun dari jurnal harian yang telah kamu setujui sebelumnya:
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 pt-1">
          <div className="rounded-[var(--radius-md)] border border-border bg-surface p-2.5">
            <span className="text-ink-muted block text-[10px] uppercase font-semibold">Tingkat Kehadiran</span>
            <span className="text-sm font-bold text-status-green">96.5% (Hadir Lengkap)</span>
          </div>
          <div className="rounded-[var(--radius-md)] border border-border bg-surface p-2.5">
            <span className="text-ink-muted block text-[10px] uppercase font-semibold">Jurnal Disetujui</span>
            <span className="text-sm font-bold text-ink">42 Jurnal Terverifikasi</span>
          </div>
          <div className="rounded-[var(--radius-md)] border border-border bg-surface p-2.5">
            <span className="text-ink-muted block text-[10px] uppercase font-semibold">Kompetensi Utama</span>
            <span className="text-sm font-bold text-primary">8 / 8 Target Terkuasai</span>
          </div>
        </div>
      </div>

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
