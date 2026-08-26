import { Card, EmptyState, Icon } from "@/components/ui";
import type { StudentLearningRecordMonitoringEventDto, StudentLearningRecordPlacementDto, StudentLearningRecordStageDto } from "@/lib/apiTypes";

const stageLabel = (stage: StudentLearningRecordStageDto["stage"]) =>
  stage === "Final" ? "Penilaian Akhir (Evaluasi Kelulusan PKL)" : "Penilaian Tengah (Evaluasi Awal Perkembangan)";

const stageDescription = (stage: StudentLearningRecordStageDto["stage"]) =>
  stage === "Final"
    ? "Penilaian komprehensif di akhir masa PKL yang menentukan pencapaian kompetensi dan kelulusan program PKL siswa."
    : "Penilaian berkala di pertengahan masa PKL untuk mengukur adaptasi dan kompetensi awal siswa di tempat kerja.";

const scoreLabel = (score: number) =>
  score === 0 ? "Belum Diisi oleh Mentor (Skor 0)" : ["", "Sangat Kurang", "Kurang", "Cukup", "Baik", "Sangat Baik"][score] ?? `Skor ${score}`;

function formatDate(value: string) {
  return new Date(value).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" });
}

export function LearningRecordOverview({ record }: { record: StudentLearningRecordPlacementDto }) {
  const currentLabel = record.progressState === "CorrectionInProgress"
    ? "Penilaian sedang diperbaiki"
    : record.currentStage ? stageLabel(record.currentStage) : null;

  return (
    <div className="flex max-w-4xl flex-col gap-5">
      <header className="flex flex-col gap-1">
        <p className="text-sm font-medium text-primary">Perkembangan selama PKL</p>
        <h1 className="text-3xl font-extrabold tracking-tight text-ink">{record.companyName}</h1>
        <p className="text-base text-ink-muted">{record.periodName} · {formatDate(record.startDate)} – {formatDate(record.endDate)}</p>
      </header>

      {currentLabel && <p className="rounded-[var(--radius-md)] border border-border/50 bg-surface-muted p-3 text-sm text-ink">Hasil terbaru: {currentLabel}</p>}

      {record.legacyFinalAssessment && <section className="border border-border bg-surface-muted p-4" aria-labelledby="legacy-assessment-title">
        <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Riwayat sebelum Learning Record V3</p>
        <h2 id="legacy-assessment-title" className="mt-1 text-lg font-semibold text-ink">Penilaian lama - hanya hasil akhir tersedia</h2>
        <p className="mt-2 text-sm text-ink-muted">Data ini berasal dari penilaian V2 dan tidak diubah menjadi penilaian Middle atau revisi V3.</p>
        <dl className="mt-3 grid gap-2 text-sm sm:grid-cols-2"><div><dt className="text-xs text-ink-muted">Skor akhir V2</dt><dd className="font-semibold text-ink">{record.legacyFinalAssessment.finalScore ?? "Tidak tercatat"}</dd></div>{record.legacyFinalAssessment.finalizedAt && <div><dt className="text-xs text-ink-muted">Difinalkan</dt><dd className="font-medium text-ink">{formatDate(record.legacyFinalAssessment.finalizedAt)}</dd></div>}</dl>
      </section>}

      {record.stages.length === 0 ? (
        <EmptyState
          icon={<Icon name="list-checks" size={32} />}
          title={record.legacyFinalAssessment ? "Belum ada hasil Learning Record V3" : "Belum ada hasil Penilaian Tengah"}
          description={record.legacyFinalAssessment ? "Hasil penilaian V2 di atas tetap terpisah. Hasil V3 akan muncul setelah penilaian resmi selesai." : "Penilaian Tengah belum difinalkan oleh Mentor Industri. Hasil akan muncul setelah penilaian resmi selesai."}
        />
      ) : (
        record.stages.map((stage) => <StageCard key={`${stage.stage}-${stage.finalizedAt}`} stage={stage} />)
      )}
      <MonitoringTimeline events={record.monitoringTimeline} />
    </div>
  );
}

const monitoringStatusLabel: Record<StudentLearningRecordMonitoringEventDto["status"], string> = {
  ProgressingAsExpected: "Berjalan sesuai rencana",
  NeedsAttention: "Perlu perhatian",
  Problem: "Ada masalah",
};

function MonitoringTimeline({ events }: { events: StudentLearningRecordMonitoringEventDto[] }) {
  return <section aria-labelledby="student-monitoring-title" className="border-t border-border pt-5">
    <div className="flex items-center justify-between gap-3"><h2 id="student-monitoring-title" className="text-xl font-semibold text-ink">Timeline monitoring</h2><span className="text-sm text-ink-muted">{events.length} catatan</span></div>
    {events.length === 0 ? <p className="mt-3 text-sm text-ink-muted">Belum ada catatan monitoring yang dibagikan.</p> : <ol className="mt-3 divide-y divide-border">{events.map((event) => <li key={event.id} className="py-3"><div className="flex flex-wrap items-start justify-between gap-2"><p className="font-medium text-ink">{monitoringStatusLabel[event.status]}</p><time className="text-xs text-ink-muted">{formatDate(event.createdAt)}</time></div>{event.note && <p className="mt-1 text-sm text-ink">{event.note}</p>}{event.followUpContext && <p className="mt-1 text-xs text-ink-muted">Tindak lanjut: {event.followUpContext}</p>}</li>)}</ol>}
  </section>;
}

function StageCard({ stage }: { stage: StudentLearningRecordStageDto }) {
  return (
    <Card title={stageLabel(stage.stage)}>
      <div className="flex flex-col gap-4">
        <dl className="grid gap-2 text-sm sm:grid-cols-2">
          <div><dt className="text-xs text-ink-muted">Difinalkan</dt><dd className="font-medium text-ink">{formatDate(stage.finalizedAt)}</dd></div>
          <div><dt className="text-xs text-ink-muted">Mentor Industri</dt><dd className="font-medium text-ink">{stage.evaluatorDisplayName}</dd></div>
        </dl>
        <p className="border-l-2 border-primary pl-3 text-sm text-ink">{stage.overallNote}</p>
        <div className="flex flex-col gap-3">
          {stage.criteria.map((criterion) => (
            <section key={criterion.criterionSnapshotId} className="border-t border-border/40 pt-3">
              <h3 className="font-semibold text-ink">{criterion.sortOrder}. {criterion.name}</h3>
              <p className="mt-1 text-sm text-ink-muted">{criterion.description}</p>
              <p className="mt-2 text-sm text-ink"><span className="font-medium">Skor {criterion.score}</span> · {scoreLabel(criterion.score)}{criterion.comment ? ` · ${criterion.comment}` : ""}</p>
              {criterion.evidence.length > 0 && (
                <div className="mt-3 rounded-[var(--radius-md)] bg-surface-muted p-3">
                  <h4 className="text-xs font-semibold text-ink">Bukti pekerjaan</h4>
                  <ul className="mt-2 flex flex-col gap-2 text-sm text-ink">
                    {criterion.evidence.map((item) => <li key={item.journalEntryId}>{item.text} <span className="text-xs text-ink-muted">· {formatDate(item.submittedAt)}</span></li>)}
                  </ul>
                </div>
              )}
            </section>
          ))}
        </div>
      </div>
    </Card>
  );
}
