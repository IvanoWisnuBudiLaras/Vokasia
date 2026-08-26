import { Icon, StatusBadge } from "@/components/ui";
import { JournalEntryStatus, ragLabel, ragToBadgeStatus, type DashboardFlaggedStudentDto, type JournalReportRowDto, type RecapRowDto } from "@/lib/apiTypes";
import { ExportButton } from "../penilaian/rekap/ExportButton";

function formatDate(value: string): string {
  return new Date(value).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" });
}

function assessmentStatus(status: RecapRowDto["status"]): { label: string; tone: "green" | "amber" | "red" } {
  if (status === "Final") return { label: "Selesai", tone: "green" };
  if (status === "Draft") return { label: "Belum selesai", tone: "amber" };
  return { label: "Belum dinilai", tone: "red" };
}

function journalStatus(status: number): { label: string; tone: "green" | "amber" | "red" } {
  if (status === JournalEntryStatus.Approved) return { label: "Disetujui", tone: "green" };
  if (status === JournalEntryStatus.Rejected) return { label: "Perlu dikirim ulang", tone: "red" };
  return { label: "Menunggu review", tone: "amber" };
}

function AssessmentRow({ row }: { row: RecapRowDto }) {
  const status = assessmentStatus(row.status);
  return (
    <>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="font-medium text-ink">{row.studentName}</p>
          <p className="text-sm text-ink-muted">{row.companyName}</p>
        </div>
        <StatusBadge status={status.tone} label={status.label} />
      </div>
      <div className="mt-3 grid grid-cols-3 gap-3 border-t border-border pt-3 text-sm">
        <div><p className="text-xs text-ink-muted">Mentor</p><p className="text-ink">{row.mentorAvg?.toFixed(2) ?? "—"}</p></div>
        <div><p className="text-xs text-ink-muted">Guru</p><p className="text-ink">{row.teacherAvg?.toFixed(2) ?? "—"}</p></div>
        <div><p className="text-xs text-ink-muted">Nilai akhir</p><p className="text-ink">{row.finalScore?.toFixed(2) ?? "—"}</p></div>
      </div>
    </>
  );
}

export function AssessmentReportTable({ periodId, rows, filtered, canExport }: { periodId: string; rows: RecapRowDto[]; filtered: boolean; canExport: boolean }) {
  return (
    <section className="flex flex-col gap-4" aria-labelledby="assessment-report-heading">
      <div className="flex flex-col gap-3 border-y border-border py-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 id="assessment-report-heading" className="text-lg font-semibold text-ink">Detail penilaian</h2>
          <p className="text-sm text-ink-muted">{rows.length} siswa pada data yang sedang ditampilkan.</p>
        </div>
        {canExport && !filtered && <ExportButton periodId={periodId} />}
      </div>

      {rows.length === 0 ? (
        <p role="status" className="border-y border-border py-8 text-center text-sm text-ink-muted">{filtered ? "Semua penilaian sudah selesai." : "Belum ada data penilaian pada periode ini."}</p>
      ) : (
        <>
          <div className="flex flex-col divide-y divide-border border-y border-border lg:hidden">
            {rows.map((row) => <div key={row.placementId} className="py-4"><AssessmentRow row={row} /></div>)}
          </div>
          <div className="hidden overflow-x-auto border border-border lg:block">
            <table className="w-full text-left text-sm">
              <thead className="bg-surface-muted text-ink"><tr>
                <th className="p-3 font-medium">Nama</th><th className="p-3 font-medium">DUDI</th>
                <th className="p-3 font-medium">Mentor</th><th className="p-3 font-medium">Guru</th>
                <th className="p-3 font-medium">Nilai akhir</th><th className="p-3 font-medium">Status</th>
              </tr></thead>
              <tbody>{rows.map((row) => { const status = assessmentStatus(row.status); return <tr key={row.placementId} className="border-t border-border">
                <td className="p-3 font-medium text-ink">{row.studentName}</td><td className="p-3 text-ink-muted">{row.companyName}</td>
                <td className="p-3 text-ink">{row.mentorAvg?.toFixed(2) ?? "—"}</td><td className="p-3 text-ink">{row.teacherAvg?.toFixed(2) ?? "—"}</td>
                <td className="p-3 text-ink">{row.finalScore?.toFixed(2) ?? "—"}</td><td className="p-3"><StatusBadge status={status.tone} label={status.label} /></td>
              </tr>; })}</tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}

function JournalRow({ row }: { row: JournalReportRowDto }) {
  const status = journalStatus(row.status);
  return (
    <>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0"><p className="font-medium text-ink">{row.studentName}</p><p className="text-sm text-ink-muted">{row.companyName}</p></div>
        <StatusBadge status={status.tone} label={status.label} />
      </div>
      <p className="mt-2 text-sm text-ink-muted">{formatDate(row.date)} · dikirim {formatDate(row.submittedAt)}</p>
      {row.mentorNote && <p className="mt-2 text-sm text-ink">Catatan mentor: {row.mentorNote}</p>}
      <a href={`/app/bimbingan/${row.placementId}`} className="mt-3 inline-flex min-h-[var(--tap-min)] items-center gap-1 text-sm font-semibold text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-focus">Lihat jurnal <Icon name="arrow-right" size={16} /></a>
    </>
  );
}

export function JournalReportTable({ rows }: { rows: JournalReportRowDto[] }) {
  return (
    <section className="flex flex-col gap-4" aria-labelledby="journal-report-heading">
      <div className="border-y border-border py-4"><h2 id="journal-report-heading" className="text-lg font-semibold text-ink">Detail jurnal</h2><p className="text-sm text-ink-muted">{rows.length} jurnal menunggu review pada periode ini.</p></div>
      {rows.length === 0 ? <p role="status" className="border-y border-border py-8 text-center text-sm text-ink-muted">Tidak ada jurnal yang menunggu review pada periode ini.</p> : <>
        <div className="flex flex-col divide-y divide-border border-y border-border lg:hidden">{rows.map((row) => <div key={row.journalId} className="py-4"><JournalRow row={row} /></div>)}</div>
        <div className="hidden overflow-x-auto border border-border lg:block"><table className="w-full text-left text-sm"><thead className="bg-surface-muted"><tr><th className="p-3 font-medium">Siswa</th><th className="p-3 font-medium">DUDI</th><th className="p-3 font-medium">Tanggal</th><th className="p-3 font-medium">Status</th><th className="p-3 font-medium">Aksi</th></tr></thead><tbody>{rows.map((row) => { const status = journalStatus(row.status); return <tr key={row.journalId} className="border-t border-border"><td className="p-3 font-medium text-ink">{row.studentName}</td><td className="p-3 text-ink-muted">{row.companyName}</td><td className="p-3 text-ink-muted">{formatDate(row.date)}</td><td className="p-3"><StatusBadge status={status.tone} label={status.label} /></td><td className="p-3"><a href={`/app/bimbingan/${row.placementId}`} className="font-semibold text-primary underline underline-offset-4">Lihat jurnal</a></td></tr>; })}</tbody></table></div>
      </>}
    </section>
  );
}

export function PriorityReportTable({ rows, periodId }: { rows: DashboardFlaggedStudentDto[]; periodId: string }) {
  return (
    <section className="flex flex-col gap-4" aria-labelledby="priority-report-heading">
      <div className="border-y border-border py-4"><h2 id="priority-report-heading" className="text-lg font-semibold text-ink">Detail siswa yang perlu perhatian</h2><p className="text-sm text-ink-muted">{rows.length} siswa ditandai dari status jurnal hari ini.</p></div>
      {rows.length === 0 ? <p role="status" className="border-y border-border py-8 text-center text-sm text-ink-muted">Semua siswa berstatus normal pada periode ini.</p> : <div className="flex flex-col divide-y divide-border border-y border-border">{rows.map((row) => <div key={row.studentId} className="flex flex-col gap-2 py-4 sm:flex-row sm:items-center sm:justify-between"><div className="flex items-start gap-3"><StatusBadge status={ragToBadgeStatus(row.rag)} label={ragLabel(row.rag)} /><div><p className="font-medium text-ink">{row.name}</p><p className="text-sm text-ink-muted">{row.companyName} · {row.reason}</p></div></div><a href={`/app?periodId=${periodId}#siswa-bermasalah`} className="inline-flex min-h-[var(--tap-min)] items-center gap-1 text-sm font-semibold text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-focus">Lihat di Ringkasan <Icon name="arrow-right" size={16} /></a></div>)}</div>}
    </section>
  );
}
