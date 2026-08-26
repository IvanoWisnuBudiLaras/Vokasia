import type { LearningRecordReportResponseDto, LearningRecordReportRowDto } from "@/lib/apiTypes";
import { TenantAdminReopenControl } from "./TenantAdminReopenControl";

function statusLabel(status: LearningRecordReportRowDto["completionStatus"]): string {
  if (status === "Finalized") return "Selesai";
  if (status === "CorrectionInProgress") return "Sedang diperbaiki";
  if (status === "InProgress") return "Sedang berjalan";
  return "Belum dimulai";
}

function stageLabel(status: LearningRecordReportRowDto["middleStatus"]): string {
  if (status === "Finalized") return "Selesai";
  if (status === "Reopened") return "Perlu diperbaiki";
  if (status === "Draft") return "Sedang diisi";
  return "Belum dimulai";
}

function monitoringLabel(status: LearningRecordReportRowDto["monitoringStatus"]): string {
  if (status === "Problem") return "Masalah";
  if (status === "NeedsAttention") return "Perlu perhatian";
  if (status === "ProgressingAsExpected") return "Sesuai rencana";
  return "Belum dicatat";
}

function date(value: string): string {
  return new Intl.DateTimeFormat("id-ID", { dateStyle: "medium" }).format(new Date(`${value}T00:00:00`));
}

function pageHref(queryString: string, page: number): string {
  const params = new URLSearchParams(queryString);
  params.set("page", String(page));
  return `/app/laporan/perkembangan?${params.toString()}`;
}

export function DevelopmentReportTable({
  report,
  queryString,
  canReopen,
}: {
  report: LearningRecordReportResponseDto;
  queryString: string;
  canReopen: boolean;
}) {
  const start = report.totalCount === 0 ? 0 : (report.page - 1) * report.pageSize + 1;
  const end = Math.min(report.totalCount, report.page * report.pageSize);

  return (
    <section aria-labelledby="development-report-results" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 id="development-report-results" className="text-lg font-semibold text-ink">Data perkembangan</h2>
          <p className="text-sm text-ink-muted">Menampilkan {start}–{end} dari {report.totalCount} penempatan.</p>
        </div>
        {report.totalPages > 1 && (
          <nav aria-label="Paginasi laporan perkembangan" className="flex items-center gap-2 text-sm">
            {report.page > 1 && <a href={pageHref(queryString, report.page - 1)} className="inline-flex min-h-[var(--tap-min)] items-center rounded-[var(--radius-md)] border border-border px-3 font-medium text-primary underline-offset-4 hover:bg-primary-muted focus-visible:outline-2 focus-visible:outline-focus">Sebelumnya</a>}
            <span className="px-1 text-ink-muted">Halaman {report.page} dari {report.totalPages}</span>
            {report.page < report.totalPages && <a aria-label="Halaman berikutnya" href={pageHref(queryString, report.page + 1)} className="inline-flex min-h-[var(--tap-min)] items-center rounded-[var(--radius-md)] border border-border px-3 font-medium text-primary underline-offset-4 hover:bg-primary-muted focus-visible:outline-2 focus-visible:outline-focus">Berikutnya</a>}
          </nav>
        )}
      </div>

      {report.findings.length > 0 && (
        <ul aria-label="Temuan laporan" className="grid gap-3 sm:grid-cols-2">
          {report.findings.map((finding) => (
            <li key={finding.kind} className="rounded-[var(--radius-lg)] border border-status-amber/30 bg-status-amber/5 p-4">
              <p className="text-sm font-semibold text-ink">{finding.label}</p>
              <p className="mt-1 text-sm text-ink-muted">{finding.count} penempatan perlu dilihat.</p>
            </li>
          ))}
        </ul>
      )}

      {report.items.length === 0 ? (
        <div role="status" className="rounded-[var(--radius-lg)] border border-dashed border-border p-8 text-center text-sm text-ink-muted">Tidak ada data yang cocok dengan filter.</div>
      ) : (
        <>
          <div className="hidden overflow-x-auto rounded-[var(--radius-lg)] border border-border sm:block">
            <table className="w-full text-left text-sm">
              <caption className="sr-only">Laporan perkembangan PKL</caption>
              <thead className="bg-surface-muted text-xs uppercase tracking-wide text-ink-muted">
                <tr>
                  <th scope="col" className="p-3">Siswa</th>
                  <th scope="col" className="p-3">DUDI</th>
                  <th scope="col" className="p-3">Periode</th>
                  <th scope="col" className="p-3">Middle</th>
                  <th scope="col" className="p-3">Final</th>
                  <th scope="col" className="p-3">Monitoring</th>
                  <th scope="col" className="p-3">Status</th>
                  {canReopen && <th scope="col" className="p-3">Aksi admin</th>}
                </tr>
              </thead>
              <tbody>
                {report.items.map((item) => (
                  <tr key={item.placementId} className="border-t border-border align-top">
                    <td className="p-3 font-medium text-ink">{item.studentName}</td>
                    <td className="p-3 text-ink-muted">{item.companyName}</td>
                    <td className="p-3 text-ink-muted"><span className="block">{item.periodName}</span><span className="text-xs">{date(item.periodStartDate)} – {date(item.periodEndDate)}</span></td>
                    <td className="p-3 text-ink">{stageLabel(item.middleStatus)}</td>
                    <td className="p-3 text-ink">{stageLabel(item.finalStatus)}</td>
                    <td className="p-3 text-ink-muted">{monitoringLabel(item.monitoringStatus)}</td>
                    <td className="p-3 text-ink">{statusLabel(item.completionStatus)}</td>
                    {canReopen && <td className="p-3"><ReopenActions item={item} /></td>}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex flex-col gap-3 sm:hidden">
            {report.items.map((item) => (
              <article key={item.placementId} className="rounded-[var(--radius-lg)] border border-border bg-surface p-4">
                <div className="flex items-start justify-between gap-3"><h3 className="font-semibold text-ink">{item.studentName}</h3><span className="text-xs font-medium text-ink-muted">{statusLabel(item.completionStatus)}</span></div>
                <p className="mt-1 text-sm text-ink-muted">{item.companyName} · {item.periodName}</p>
                <dl className="mt-4 grid grid-cols-2 gap-3 text-sm"><div><dt className="text-xs text-ink-muted">Middle</dt><dd className="font-medium text-ink">{stageLabel(item.middleStatus)}</dd></div><div><dt className="text-xs text-ink-muted">Final</dt><dd className="font-medium text-ink">{stageLabel(item.finalStatus)}</dd></div><div className="col-span-2"><dt className="text-xs text-ink-muted">Monitoring</dt><dd className="font-medium text-ink">{monitoringLabel(item.monitoringStatus)}</dd></div></dl>
                {canReopen && <div className="mt-4"><ReopenActions item={item} /></div>}
              </article>
            ))}
          </div>
        </>
      )}
    </section>
  );
}

function ReopenActions({ item }: { item: LearningRecordReportRowDto }) {
  return <div className="flex flex-wrap gap-2">{item.middleStatus === "Finalized" && <TenantAdminReopenControl placementId={item.placementId} stage="Middle" />}{item.finalStatus === "Finalized" && <TenantAdminReopenControl placementId={item.placementId} stage="Final" />}</div>;
}
