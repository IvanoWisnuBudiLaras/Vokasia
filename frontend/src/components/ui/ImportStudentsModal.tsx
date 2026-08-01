"use client";

import { useState, useMemo } from "react";
import { Button } from "./Button";

export interface TenantOption {
  id: string;
  name: string;
}

export interface ImportStudentsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
  tenants?: TenantOption[];
}

interface ParsedPreviewRow {
  rowNumber: number;
  fullName: string;
  nisn: string;
  majorName: string;
  classroom: string;
  errors: string[];
}

export function ImportStudentsModal({
  isOpen,
  onClose,
  onSuccess,
  tenants = [],
}: ImportStudentsModalProps) {
  const [selectedTenantId, setSelectedTenantId] = useState<string>(tenants[0]?.id ?? "");
  const [file, setFile] = useState<File | null>(null);
  const [fileContent, setFileContent] = useState<string>("");
  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState<{
    imported: number;
    errors: Array<{ rowNumber: number; column: string; error: string }>;
  } | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [isDryRunExecuted, setIsDryRunExecuted] = useState(false);

  if (!isOpen) return null;

  // Client-side quick preview parser for line-by-line preview
  const parsedRows = useMemo(() => {
    if (!fileContent) return [];
    const lines = fileContent.split(/\r?\n/).filter((line) => line.trim().length > 0);
    if (lines.length <= 1) return [];

    const headers = lines[0].split(",").map((h) => h.trim().toLowerCase());
    const idx = (name: string) => headers.findIndex((h) => h.includes(name));
    const [iName, iNisn, iMajor, iClass] = [
      idx("fullname") >= 0 ? idx("fullname") : idx("nama"),
      idx("nisn"),
      idx("majorname") >= 0 ? idx("majorname") : idx("jurusan"),
      idx("classroom") >= 0 ? idx("classroom") : idx("kelas"),
    ];

    const rows: ParsedPreviewRow[] = [];
    for (let r = 1; r < lines.length; r++) {
      const cols = lines[r].split(",").map((c) => c.trim());
      const fullName = cols[iName >= 0 ? iName : 0] ?? "";
      const nisn = cols[iNisn >= 0 ? iNisn : 1] ?? "";
      const majorName = cols[iMajor >= 0 ? iMajor : 2] ?? "";
      const classroom = cols[iClass >= 0 ? iClass : 3] ?? "";

      const rowErrors: string[] = [];
      if (!fullName) rowErrors.push("Nama Siswa (FullName) wajib diisi");
      if (!majorName) rowErrors.push("Jurusan (MajorName) wajib diisi");
      if (!classroom) rowErrors.push("Kelas (Classroom) wajib diisi");

      rows.push({
        rowNumber: r + 1,
        fullName: fullName || "(Kosong)",
        nisn: nisn || "—",
        majorName: majorName || "(Kosong)",
        classroom: classroom || "(Kosong)",
        errors: rowErrors,
      });
    }

    return rows;
  }, [fileContent]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0] ?? null;
    setFile(selected);
    setResult(null);
    setErrorMsg(null);
    setSuccessMsg(null);
    setIsDryRunExecuted(false);

    if (selected) {
      const reader = new FileReader();
      reader.onload = (evt) => {
        setFileContent((evt.target?.result as string) || "");
      };
      reader.readAsText(selected);
    } else {
      setFileContent("");
    }
  };

  const handleDownloadTemplate = () => {
    const csvContent =
      "FullName,Nisn,MajorName,Classroom\n" +
      "Ivano Wisnu Budi Laras,0081234567,Teknik Komputer dan Jaringan,XII TKJ 1\n" +
      "Ahmad Rizky Pratama,0081234568,Rekayasa Perangkat Lunak,XII RPL 1\n" +
      "Dewi Maharani,0081234569,Desain Komunikasi Visual,XII DKV 2\n";
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", "template_import_siswa_dapodik.csv");
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const handleUpload = async (dryRun: boolean) => {
    if (!file) {
      setErrorMsg("Pilih file CSV terlebih dahulu.");
      return;
    }

    setIsLoading(true);
    setErrorMsg(null);
    setSuccessMsg(null);

    try {
      const formData = new FormData();
      formData.append("file", file);
      if (selectedTenantId) {
        formData.append("tenantId", selectedTenantId);
      }

      const queryParams = new URLSearchParams({ dryRun: String(dryRun) });
      if (selectedTenantId) {
        queryParams.set("tenantId", selectedTenantId);
      }

      const res = await fetch(`/api/proxy/api/students/import?${queryParams.toString()}`, {
        method: "POST",
        body: formData,
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(`Gagal mengunggah file (${res.status}): ${text}`);
      }

      const data = await res.json();
      setResult(data);
      if (dryRun) {
        setIsDryRunExecuted(true);
      } else {
        setSuccessMsg(`🎉 Berhasil mengimpor ${data.imported ?? parsedRows.length} data siswa ke dalam sistem!`);
        if (onSuccess) onSuccess();
      }
    } catch (err: any) {
      setErrorMsg(err.message || "Terjadi kesalahan saat memproses file CSV.");
    } finally {
      setIsLoading(false);
    }
  };

  const totalRows = parsedRows.length;
  const invalidRowsCount = parsedRows.filter((r) => r.errors.length > 0).length + (result?.errors.length ?? 0);
  const validRowsCount = Math.max(0, totalRows - invalidRowsCount);

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm animate-fade-in">
      <div className="w-full max-w-2xl max-h-[90vh] flex flex-col rounded-[var(--radius-lg)] border border-border bg-surface shadow-2xl overflow-hidden">
        {/* Modal Header */}
        <div className="flex items-center justify-between border-b border-border px-6 py-4 bg-surface-muted/50">
          <div>
            <h3 className="text-lg font-bold text-ink flex items-center gap-2">
              📥 Import Data Siswa (Dapodik CSV)
            </h3>
            <p className="text-xs text-ink-muted">
              Wizard pengunggahan data siswa massal dengan simulasi validasi (Dry Run) & penanganan error.
            </p>
          </div>
          <button
            onClick={onClose}
            className="text-ink-muted hover:text-ink text-xl font-bold p-1 rounded-md transition-colors"
          >
            ✕
          </button>
        </div>

        {/* Modal Body */}
        <div className="flex-1 overflow-y-auto p-6 space-y-5">
          {/* Guide Banner */}
          <div className="rounded-[var(--radius-md)] border border-primary/20 bg-primary/5 p-4 text-xs leading-relaxed text-ink">
            <div className="flex items-center justify-between mb-2">
              <span className="font-semibold text-primary uppercase tracking-wider text-[11px]">
                📌 Ketentuan Format Kolom CSV
              </span>
              <button
                type="button"
                onClick={handleDownloadTemplate}
                className="inline-flex items-center gap-1 text-xs font-semibold text-primary underline hover:text-primary/80"
              >
                📥 Unduh Template CSV Dapodik
              </button>
            </div>
            <ul className="list-disc pl-4 space-y-1 text-ink-muted">
              <li>
                Header kolom wajib: <code className="font-mono font-semibold text-primary bg-primary/10 px-1 rounded">FullName</code>,{" "}
                <code className="font-mono font-semibold text-primary bg-primary/10 px-1 rounded">MajorName</code>,{" "}
                <code className="font-mono font-semibold text-primary bg-primary/10 px-1 rounded">Classroom</code>
              </li>
              <li>
                Header opsional: <code className="font-mono font-semibold text-primary bg-primary/10 px-1 rounded">Nisn</code>
              </li>
              <li>Sistem akan otomatis menyesuaikan jurusan apabila belum terdaftar.</li>
            </ul>
          </div>

          {/* Tenant Selector for SuperAdmin view */}
          {tenants.length > 0 && (
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-semibold text-ink">Target Sekolah (Tenant):</label>
              <select
                value={selectedTenantId}
                onChange={(e) => setSelectedTenantId(e.target.value)}
                className="h-[var(--tap-min)] w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus:border-primary"
              >
                {tenants.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name}
                  </option>
                ))}
              </select>
            </div>
          )}

          {/* File Input */}
          <div className="flex flex-col gap-1.5">
            <label className="text-xs font-semibold text-ink">Pilih File CSV:</label>
            <input
              type="file"
              accept=".csv"
              onChange={handleFileChange}
              className="w-full text-sm text-ink file:mr-4 file:rounded-[var(--radius-md)] file:border-0 file:bg-primary file:px-4 file:py-2 file:text-xs file:font-semibold file:text-primary-ink hover:file:bg-primary/90 cursor-pointer"
            />
          </div>

          {/* Error / Success Messages */}
          {errorMsg && (
            <div className="rounded-[var(--radius-md)] border border-status-red/30 bg-status-red-bg p-3.5 text-xs text-status-red flex items-start gap-2">
              <span>⚠️</span>
              <span>{errorMsg}</span>
            </div>
          )}

          {successMsg && (
            <div className="rounded-[var(--radius-md)] border border-status-green/30 bg-status-green-bg p-3.5 text-xs text-status-green font-medium">
              {successMsg}
            </div>
          )}

          {/* Dry Run & Row Preview Summary */}
          {parsedRows.length > 0 && (
            <div className="space-y-3">
              <div className="flex items-center justify-between border-b border-border pb-2">
                <span className="text-xs font-bold text-ink uppercase tracking-wider">
                  Hasil Analisis Pratinjau ({parsedRows.length} Baris)
                </span>
                <div className="flex items-center gap-2">
                  <span className="inline-flex items-center rounded-full bg-surface-muted px-2.5 py-0.5 text-xs font-medium text-ink-muted border border-border">
                    Total: {totalRows}
                  </span>
                  <span className="inline-flex items-center rounded-full bg-status-green-bg px-2.5 py-0.5 text-xs font-medium text-status-green border border-status-green/20">
                    Valid: {validRowsCount}
                  </span>
                  {invalidRowsCount > 0 && (
                    <span className="inline-flex items-center rounded-full bg-status-red-bg px-2.5 py-0.5 text-xs font-medium text-status-red border border-status-red/20">
                      Error: {invalidRowsCount}
                    </span>
                  )}
                </div>
              </div>

              {/* Preview Table */}
              <div className="max-h-56 overflow-y-auto rounded-[var(--radius-md)] border border-border bg-surface">
                <table className="w-full text-left text-xs">
                  <thead className="sticky top-0 bg-surface-muted font-medium text-ink-muted border-b border-border">
                    <tr>
                      <th className="p-2.5 w-14">Baris</th>
                      <th className="p-2.5">Nama Siswa</th>
                      <th className="p-2.5">NISN</th>
                      <th className="p-2.5">Jurusan</th>
                      <th className="p-2.5">Kelas</th>
                      <th className="p-2.5">Status / Validasi</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {parsedRows.map((row) => {
                      const hasError = row.errors.length > 0;
                      return (
                        <tr
                          key={row.rowNumber}
                          className={hasError ? "bg-status-red-bg/30" : "hover:bg-surface-muted/50"}
                        >
                          <td className="p-2.5 font-mono text-ink-muted">{row.rowNumber}</td>
                          <td className="p-2.5 font-medium text-ink">{row.fullName}</td>
                          <td className="p-2.5 text-ink-muted">{row.nisn}</td>
                          <td className="p-2.5 text-ink-muted">{row.majorName}</td>
                          <td className="p-2.5 text-ink-muted">{row.classroom}</td>
                          <td className="p-2.5">
                            {hasError ? (
                              <span className="text-status-red font-medium">
                                ❌ {row.errors.join(", ")}
                              </span>
                            ) : (
                              <span className="text-status-green font-medium">✅ Valid</span>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Backend Dry Run Error List */}
          {result?.errors && result.errors.length > 0 && (
            <div className="rounded-[var(--radius-md)] border border-status-red/30 bg-status-red-bg p-3 text-xs space-y-1">
              <p className="font-bold text-status-red">
                Temuan Validasi Backend ({result.errors.length} Error):
              </p>
              <div className="max-h-32 overflow-y-auto space-y-1 pl-1">
                {result.errors.map((err, idx) => (
                  <p key={idx} className="text-status-red/90 font-mono">
                    Baris {err.rowNumber} [{err.column}]: {err.error}
                  </p>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Modal Footer Actions */}
        <div className="flex flex-wrap items-center justify-between gap-3 border-t border-border px-6 py-4 bg-surface-muted/30">
          <button
            type="button"
            onClick={handleDownloadTemplate}
            className="text-xs font-semibold text-primary underline hover:text-primary/80"
          >
            📥 Unduh Template CSV
          </button>

          <div className="flex items-center gap-3">
            <Button variant="secondary" size="md" onClick={onClose}>
              Batal / Tutup
            </Button>
            <Button
              variant="secondary"
              size="md"
              disabled={isLoading || !file}
              onClick={() => handleUpload(true)}
            >
              {isLoading ? "Memeriksa…" : "🔍 Simulasi Validasi (Dry Run)"}
            </Button>
            <Button
              variant="primary"
              size="md"
              disabled={isLoading || !file || invalidRowsCount > 0}
              onClick={() => handleUpload(false)}
            >
              {isLoading ? "Mengunggah…" : "🚀 Import Sekarang"}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
