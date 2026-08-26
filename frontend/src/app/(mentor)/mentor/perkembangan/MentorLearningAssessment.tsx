"use client";

import { useState } from "react";
import { ApprovedEvidencePicker } from "@/components/learning-record/ApprovedEvidencePicker";
import { AssessmentStageStatus } from "@/components/learning-record/AssessmentStageStatus";
import { Button, Textarea } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import { useFormDraft } from "@/lib/useFormDraft";
import type { LearningAssessmentDraftInput, LearningAssessmentDto } from "@/lib/apiTypes";

export function toAssessmentDraft(assessment: LearningAssessmentDto): LearningAssessmentDraftInput {
  return {
    overallNote: assessment.overallNote ?? "",
    criteria: assessment.criteria.map((item) => ({
      criterionSnapshotId: item.criterionSnapshotId,
      score: item.score,
      comment: item.comment ?? "",
      journalEntryIds: item.evidence.map((evidence) => evidence.journalEntryId),
    })),
  };
}

export function validateLearningAssessmentDraft(draft: LearningAssessmentDraftInput) {
  const criterionErrors = Object.fromEntries(draft.criteria.filter((item) => item.score === null).map((item) => [item.criterionSnapshotId, "Pilih skor 1 sampai 5."]));
  return { criterionErrors, overallNoteError: draft.overallNote.trim() ? undefined : "Catatan keseluruhan wajib diisi." };
}

export function toggleEvidenceSelection(selectedIds: string[], journalEntryId: string): string[] {
  return selectedIds.includes(journalEntryId)
    ? selectedIds.filter((value) => value !== journalEntryId)
    : [...selectedIds, journalEntryId];
}

export function MentorLearningAssessment({ assessment: initialAssessment }: { assessment: LearningAssessmentDto }) {
  const [assessment, setAssessment] = useState(initialAssessment);
  const { values: draft, setValues: setDraft, clearDraft } = useFormDraft(
    `mentor_assessment_${initialAssessment.placementId}_${initialAssessment.stage}`,
    toAssessmentDraft(initialAssessment)
  );
  const [errors, setErrors] = useState(() => validateLearningAssessmentDraft({ ...toAssessmentDraft(initialAssessment), overallNote: initialAssessment.overallNote ?? "isi" }));
  const [message, setMessage] = useState<string | null>(null);
  const locked = assessment.status === "Finalized";

  const updateCriterion = (index: number, patch: Partial<LearningAssessmentDraftInput["criteria"][number]>) =>
    setDraft((current) => ({ ...current, criteria: current.criteria.map((item, itemIndex) => itemIndex === index ? { ...item, ...patch } : item) }));
  const persist = async (finalize: boolean) => {
    const validation = validateLearningAssessmentDraft(draft);
    if (finalize && (Object.keys(validation.criterionErrors).length || validation.overallNoteError)) { setErrors(validation); return; }
    try {
      const path = `/placements/${assessment.placementId}/learning-assessments/${assessment.stage}`;
      const saved = await apiClient.put<LearningAssessmentDto>(`${path}/draft`, draft);
      const result = finalize ? await apiClient.post<LearningAssessmentDto>(`${path}/finalize`) : saved;
      setAssessment(result); setDraft(toAssessmentDraft(result)); setErrors({ criterionErrors: {}, overallNoteError: undefined });
      if (finalize) clearDraft();
      setMessage(finalize ? "Penilaian berhasil difinalkan." : "Draft berhasil disimpan.");
    } catch (error) { setMessage(error instanceof Error ? error.message : "Penilaian belum bisa disimpan."); }
  };

  return <div className="mx-auto flex max-w-4xl flex-col gap-5">
    <AssessmentStageStatus stage={assessment.stage} status={assessment.status} operationalStateLabel={assessment.operationalStateLabel} />
    {assessment.stage === "Final" && <p className="rounded-[var(--radius-md)] border border-border/50 bg-surface-muted p-3 text-sm text-ink">{assessment.middleContext?.available ? "Penilaian Tengah sudah selesai. Isi skor Akhir secara mandiri." : "Penilaian Tengah belum selesai. Penilaian Akhir tetap dapat diisi."}</p>}
    {locked && <p role="status" className="rounded-[var(--radius-md)] bg-status-green/10 p-3 text-sm text-ink">Penilaian ini sudah difinalkan dan terkunci.</p>}
    {assessment.status === "Reopened" && <p role="status" className="rounded-[var(--radius-md)] bg-status-amber/10 p-3 text-sm text-ink">Perlu perbaikan. Perbarui penilaian lalu finalkan kembali.</p>}
    {assessment.criteria.map((criterion, index) => <section key={criterion.criterionSnapshotId} className="rounded-[var(--radius-lg)] border border-border/50 bg-surface p-4 sm:p-5">
      <h2 className="text-base font-semibold text-ink">{criterion.sortOrder}. {criterion.name}</h2><p className="mt-1 text-sm text-ink-muted">{criterion.description}</p>
      <fieldset className="mt-4" disabled={locked}><legend className="text-sm font-medium text-ink">Skor 1 sampai 5</legend><div className="mt-2 flex flex-wrap gap-2">{[1,2,3,4,5].map((score) => <label key={score} className="cursor-pointer"><input className="peer sr-only" type="radio" name={criterion.criterionSnapshotId} checked={draft.criteria[index].score === score} onChange={() => updateCriterion(index, { score })} /><span className="inline-flex h-11 min-w-11 items-center justify-center rounded-[var(--radius-md)] border border-border px-3 text-sm font-semibold peer-checked:bg-primary">{score}</span></label>)}</div>{errors.criterionErrors[criterion.criterionSnapshotId] && <p className="mt-2 text-sm text-status-red">{errors.criterionErrors[criterion.criterionSnapshotId]}</p>}</fieldset>
      <Textarea label="Komentar Mentor (opsional)" maxLength={1000} disabled={locked} value={draft.criteria[index].comment} onChange={(event) => updateCriterion(index, { comment: event.target.value })} />
      <ApprovedEvidencePicker criterionName={criterion.name} candidates={assessment.evidenceCandidates} selectedIds={draft.criteria[index].journalEntryIds} disabled={locked} onToggle={(id) => updateCriterion(index, { journalEntryIds: toggleEvidenceSelection(draft.criteria[index].journalEntryIds, id) })} />
    </section>)}
    <Textarea label="Catatan keseluruhan" maxLength={1500} disabled={locked} value={draft.overallNote} error={errors.overallNoteError} onChange={(event) => setDraft((current) => ({ ...current, overallNote: event.target.value }))} />
    {message && <p role="status" className="text-sm text-ink">{message}</p>}
    {!locked && <div className="sticky bottom-0 flex flex-col gap-2 border-t border-border/40 bg-surface py-3 sm:flex-row sm:justify-end"><Button type="button" variant="secondary" onClick={() => persist(false)}>Simpan draft</Button><Button type="button" onClick={() => persist(true)}>Finalkan penilaian</Button></div>}
  </div>;
}
