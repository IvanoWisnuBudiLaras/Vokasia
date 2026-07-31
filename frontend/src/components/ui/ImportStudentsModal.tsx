"use client";

import { useState } from "react";
import { Button } from "./Button";

export interface ImportStudentsModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

export function ImportStudentsModal({ isOpen, onClose, onSuccess }: ImportStudentsModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState<{ imported: number; errors: Array<{ rowNumber: number; column: string; error: string }> } | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleDownloadTemplate = () => {
    const csvContent = "FullName,Nisn,MajorName,Classroom\nIvano Wisnu Budi Laras,0081234567,Teknik Komputer dan Jaringan,XII TKJ 1\nAhmad Rizky Pratama,0081234568,Rekayasa Perangkat Lunak,XII RPL 1\n";
    const blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.setAttribute("href", url);
    link.setAttribute("download", "template_import_siswa.csv");
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
    setResult(null);

    try {
      const formData = new FormData();
      formData.append("file", file);

      const res = await fetch(`/api/proxy/api/students/import?dryRun=${dryRun}`, {
        method: "POST",
        body: formData,
      });

      if (!res.ok) {
        const text = await res.text();
        throw new Error(`Gagal mengunggah file (${res.status}): ${text}`);
      }

      const data = await res.json();
      setResult(data);

      if (!dryRun && data.imported > 0) {
        if (onSuccess) onSuccess();
      }
    } catch (err: any) {
      setErrorMsg(err.message || "Terjadi kesalahan saat memproses file CSV.");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-[var(--radius-lg)] border border-border bg-surface p-6 shadow-xl">
        <div className="flex items-center justify-between border-b border-border pb-3">
          <h3 className="text-lg font-semibold text-ink">Import Data Siswa dari CSV</h3>
          <button onClick={onClose} className="text-ink-muted hover:text-ink text-lg font-bold">
            ✕
          </button>
        </div>

        <div className="mt-4 space-y-4">
          <div className="rounded-[var(--radius-md)] border border-primary/20 bg-primary/5 p-3 text-xs leading-relaxed text-ink-muted">
            <p className="font-medium text-ink">Ketentuan Format Kolom CSV (Dapodik):</p>
            <ul className="mt-1 list-disc pl-4 space-y-0.5">
              <li>Header wajib: <code className="font-semibold text-primary">FullName</code>, <code className="font-semibold text-primary">MajorName</code>, <code className="font-semibold text-primary">Classroom</code></li>
              <li>Header opsional: <code className="font-semibold text-primary">Nisn</code></li>
              <li>Jurusan baru (<code className="font-semibold">MajorName</code>) akan dibuat otomatis jika belum ada.</li>
            </ul>
          </div>

          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold text-ink-muted">File CSV</span>
            <button
              type="button"
              onClick={handleDownloadTemplate}
              className="text-xs text-primary underline hover:text-primary/80 font-medium"
            >
              📥 Unduh Template CSV
            </button>
          </div>

          <input
            type="file"
            accept=".csv"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="w-full text-sm text-ink file:mr-3 file:rounded-[var(--radius-md)] file:border-0 file:bg-surface-muted file:px-4 file:py-2 file:text-xs file:font-semibold file:text-ink hover:file:bg-border"
          />

          {errorMsg && (
            <p className="rounded-[var(--radius-md)] bg-status-red-bg p-3 text-xs text-status-red">
              {errorMsg}
            </p>
          )}

          {result && (
            <div className="rounded-[var(--radius-md)] border border-border bg-surface-muted p-3 text-xs">
              <p className="font-semibold text-ink">
                Status: {result.imported} data siswa berhasil diproses.
              </p>
              {result.errors.length > 0 && (
                <div className="mt-2 max-h-32 overflow-y-auto space-y-1">
                  <p className="text-status-red font-medium">Temuan Error ({result.errors.length}):</p>
                  {result.errors.map((err, idx) => (
                    <p key={idx} className="text-status-red/90">
                      Baris {err.rowNumber} [{err.column}]: {err.error}
                    </p>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>

        <div className="mt-6 flex flex-wrap items-center justify-end gap-3 border-t border-border pt-4">
          <Button variant="secondary" size="md" onClick={onClose}>
            Tutup
          </Button>
          <Button
            variant="secondary"
            size="md"
            disabled={isLoading || !file}
            onClick={() => handleUpload(true)}
          >
            {isLoading ? "Memproses…" : "Uji Coba Validasi (Dry Run)"}
          </Button>
          <Button
            variant="primary"
            size="md"
            disabled={isLoading || !file}
            onClick={() => handleUpload(false)}
          >
            {isLoading ? "Mengunggah…" : "Import Sekarang"}
          </Button>
        </div>
      </div>
    </div>
  );
}
