"use client";

import { useState, type FormEvent } from "react";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { LearningRecordReportExportStatusDto, LearningRecordReportResponseDto } from "@/lib/apiTypes";

type ExportFormat = "Pdf" | "Xlsx";
type ExportScope = "CurrentFilters" | "CurrentPage";

export function DevelopmentReportExportForm({
  report,
  queryString,
}: {
  report: LearningRecordReportResponseDto;
  queryString: string;
}) {
  const [format, setFormat] = useState<ExportFormat>("Pdf");
  const [scope, setScope] = useState<ExportScope>("CurrentFilters");
  const [quantity, setQuantity] = useState("100");
  const [message, setMessage] = useState<string | null>(null);
  const [downloadUrl, setDownloadUrl] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setDownloadUrl(null);
    setMessage("Export sedang dibuat…");
    try {
      const params = new URLSearchParams(queryString);
      const valueOrNull = (key: string) => params.get(key) || null;
      const quantityValue = quantity === "all" ? null : Number(quantity);
      const accepted = await apiClient.post<{ exportId: string }>("/teacher/learning-record/report/export", {
        format,
        scope,
        quantity: quantityValue,
        page: report.page,
        pageSize: report.pageSize,
        periodId: valueOrNull("periodId"),
        companyId: valueOrNull("companyId"),
        stage: valueOrNull("stage"),
        status: valueOrNull("status"),
        monitoringStatus: valueOrNull("monitoringStatus"),
        search: valueOrNull("search"),
        sort: valueOrNull("sort") ?? "studentName",
        direction: valueOrNull("direction") ?? "asc",
      });

      for (let attempt = 0; attempt < 30; attempt += 1) {
        await new Promise((resolve) => setTimeout(resolve, 500));
        const status = await apiClient.get<LearningRecordReportExportStatusDto>(`/teacher/learning-record/report/export/${accepted.exportId}`);
        if (status.status === "Completed") {
          setDownloadUrl(`/api/proxy/teacher/learning-record/report/export/${accepted.exportId}/download`);
          setMessage("Export siap diunduh.");
          return;
        }
        if (status.status === "Failed") {
          setMessage("Export gagal dibuat. Coba lagi dengan jumlah data lebih kecil.");
          return;
        }
      }
      setMessage("Export masih diproses. Coba buka notifikasi sebentar lagi.");
    } catch (error) {
      setMessage(error instanceof ApiError ? error.message : "Export belum bisa dibuat. Coba lagi.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-labelledby="development-report-export" className="rounded-[var(--radius-lg)] border border-border bg-surface p-4 lg:p-5">
      <div className="mb-4"><h2 id="development-report-export" className="text-lg font-semibold text-ink">Export laporan</h2><p className="text-sm text-ink-muted">Buat PDF yang ringkas atau Excel untuk data yang sedang kamu lihat.</p></div>
      <form onSubmit={handleSubmit} className="grid gap-4 md:grid-cols-4">
        <label className="flex flex-col gap-1.5 text-sm font-medium text-ink">Format<select value={format} onChange={(event) => setFormat(event.target.value as ExportFormat)} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 font-normal outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="Pdf">PDF</option><option value="Xlsx">Excel / XLSX</option></select></label>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-ink">Cakupan<select value={scope} onChange={(event) => setScope(event.target.value as ExportScope)} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 font-normal outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="CurrentFilters">Filter saat ini</option><option value="CurrentPage">Halaman saat ini</option></select></label>
        <label className="flex flex-col gap-1.5 text-sm font-medium text-ink">Jumlah baris<select value={quantity} onChange={(event) => setQuantity(event.target.value)} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface-paper px-3 font-normal outline-none focus-visible:outline-2 focus-visible:outline-focus"><option value="25">25</option><option value="50">50</option><option value="100">100</option><option value="250">250</option><option value="500">500</option><option value="all" disabled={format === "Pdf"}>Semua (Excel)</option></select></label>
        <div className="flex items-end"><button type="submit" disabled={busy} className="inline-flex min-h-[var(--tap-min)] w-full items-center justify-center rounded-[var(--radius-md)] bg-primary px-4 text-sm font-semibold text-on-primary outline-none hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-focus disabled:cursor-wait disabled:opacity-60">{busy ? "Membuat export…" : "Buat export"}</button></div>
      </form>
      {message && <p role="status" className="mt-3 text-sm text-ink-muted">{message}</p>}
      {downloadUrl && <a href={downloadUrl} download className="mt-2 inline-flex min-h-[var(--tap-min)] items-center font-semibold text-primary underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-focus">Unduh {format === "Pdf" ? "PDF" : "XLSX"}</a>}
    </section>
  );
}
