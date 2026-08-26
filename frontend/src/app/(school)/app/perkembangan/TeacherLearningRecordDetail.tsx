import type { TeacherLearningRecordPlacementDto } from "@/lib/apiTypes";

function formatDate(value: string) {
  return new Date(value).toLocaleDateString("id-ID");
}

export function TeacherLearningRecordDetail({ detail }: { detail: TeacherLearningRecordPlacementDto }) {
  return <section className="border border-border bg-surface p-5" aria-labelledby="teacher-learning-record-detail-title">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p className="text-sm font-medium text-primary">Learning Record</p>
        <h2 id="teacher-learning-record-detail-title" className="text-xl font-semibold text-ink">Detail penilaian</h2>
        <p className="mt-1 text-sm text-ink-muted">{detail.studentName} Â· {detail.companyName} Â· {detail.periodName}</p>
      </div>
      <p className="text-right text-xs text-ink-muted">{detail.startDate} â€” {detail.endDate}</p>
    </div>
    <div className="mt-5 grid gap-4 xl:grid-cols-2">
      {detail.stages.map((stage) => <article key={stage.stage} className="border border-border p-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div><h3 className="font-semibold text-ink">{stage.stage === "Middle" ? "Penilaian Tengah" : "Penilaian Akhir"}</h3><p className="text-sm text-ink-muted">{stage.status} Â· {stage.operationalStateLabel}</p></div>
          {stage.finalizedAt && <time className="text-xs text-ink-muted">Final {formatDate(stage.finalizedAt)}</time>}
        </div>
        {stage.evaluatorDisplayName && <p className="mt-3 text-sm text-ink-muted">Evaluator: <span className="font-medium text-ink">{stage.evaluatorDisplayName}</span></p>}
        {stage.overallNote && <p className="mt-3 border-l-2 border-primary pl-3 text-sm text-ink">{stage.overallNote}</p>}
        {stage.criteria.length === 0 ? <p className="mt-4 text-sm text-ink-muted">Belum ada rincian kriteria.</p> : <div className="mt-4 flex flex-col gap-3">{stage.criteria.map((criterion) => <div key={criterion.criterionSnapshotId} className="border-t border-border pt-3">
          <div className="flex flex-wrap items-baseline justify-between gap-2"><h4 className="font-medium text-ink">{criterion.name}</h4><span className="text-sm font-semibold text-ink">Skor: {criterion.score ?? "Belum diisi"}</span></div>
          {criterion.description && <p className="mt-1 text-xs text-ink-muted">Rubrik: {criterion.description}</p>}
          {criterion.comment && <p className="mt-2 text-sm text-ink">Komentar: {criterion.comment}</p>}
          {criterion.evidence.length > 0 && <div className="mt-2"><p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">Evidence terpilih</p><ul className="mt-1 flex flex-col gap-1">{criterion.evidence.map((evidence) => <li key={evidence.journalEntryId} className="text-sm text-ink">{evidence.text} <time className="text-xs text-ink-muted">({formatDate(evidence.submittedAt)})</time></li>)}</ul></div>}
        </div>)}</div>}
      </article>)}
    </div>
    <p className="mt-4 text-xs text-ink-muted">Baca saja untuk Guru. Perubahan skor, komentar, dan evidence dilakukan melalui alur Mentor Industri.</p>
  </section>;
}
