import type { LearningAssessmentStage, LearningAssessmentStatus } from "@/lib/apiTypes";

export function AssessmentStageStatus({
  stage,
  status,
  operationalStateLabel,
}: {
  stage: LearningAssessmentStage;
  status: LearningAssessmentStatus;
  operationalStateLabel: string;
}) {
  const stageLabel = stage === "Middle" ? "Penilaian Tengah" : "Penilaian Akhir";
  const statusLabel = status === "Finalized" ? "Terkunci" : status === "Reopened" ? "Perlu perbaikan" : operationalStateLabel;
  return (
    <div className="flex flex-wrap items-center gap-2" aria-label={`${stageLabel}: ${statusLabel}`}>
      <span className="rounded-full bg-surface-muted px-2.5 py-1 text-xs font-semibold text-ink">{stageLabel}</span>
      <span className="rounded-full bg-brand-soft px-2.5 py-1 text-xs font-medium text-ink">{statusLabel}</span>
    </div>
  );
}
