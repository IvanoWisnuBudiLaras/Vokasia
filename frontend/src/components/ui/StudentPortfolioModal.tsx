"use client";

import { useCallback, useEffect, useState } from "react";
import { Button, Icon, StatusBadge } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import type { PortfolioDto } from "@/lib/apiTypes";

export interface StudentPortfolioModalProps {
  studentId: string;
  studentName: string;
  isOpen: boolean;
  onClose: () => void;
}

export function StudentPortfolioModal({ studentId, studentName, isOpen, onClose }: StudentPortfolioModalProps) {
  const [data, setData] = useState<PortfolioDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);

  const loadPortfolio = useCallback(async (isMounted: () => boolean) => {
    setLoading(true);
    setError(false);

    try {
      const res = await apiClient.get<PortfolioDto>(`/portfolio/student/${studentId}`);
      if (isMounted()) setData(res);
    } catch {
      if (isMounted()) setError(true);
    } finally {
      if (isMounted()) setLoading(false);
    }
  }, [studentId]);

  useEffect(() => {
    if (!isOpen || !studentId) return;

    let isMounted = true;
    queueMicrotask(() => {
      if (isMounted) void loadPortfolio(() => isMounted);
    });

    return () => {
      isMounted = false;
    };
  }, [isOpen, loadPortfolio, studentId]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <button
        type="button"
        aria-label="Tutup modal portofolio"
        onClick={onClose}
        className="absolute inset-0 bg-ink/40 backdrop-blur-xs transition-opacity"
      />

      <div className="relative flex max-h-[85vh] w-full max-w-2xl flex-col rounded-[var(--radius-lg)] border border-border bg-surface shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border p-4">
          <div className="flex items-center gap-2">
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary-muted text-primary">
              <Icon name="briefcase-business" size={20} />
            </div>
            <div>
              <h2 className="text-base font-semibold text-ink">Portofolio Siswa — {studentName}</h2>
              <p className="text-xs text-ink-muted">Rekam jejak kompetensi & sampel jurnal terverifikasi</p>
            </div>
          </div>
          <Button variant="secondary" size="md" onClick={onClose} aria-label="Tutup">
            Tutup
          </Button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-5">
          {loading && <p className="text-center text-sm text-ink-muted">Memuat portofolio siswa…</p>}

          {error && (
            <div className="rounded-[var(--radius-md)] bg-status-red-muted p-4 text-center text-sm text-status-red">
              Gagal memuat portofolio. Siswa ini mungkin belum mengonfigurasi portofolionya.
            </div>
          )}

          {!loading && !error && data && (
            <div className="flex flex-col gap-6">
              {/* Headline */}
              <div className="rounded-[var(--radius-md)] bg-surface-muted p-4 border border-border">
                <span className="text-xs font-semibold uppercase tracking-wider text-ink-muted">Headline / Profil</span>
                <p className="mt-1 text-sm font-medium text-ink">{data.headline || "Belum ada headline profil yang diisi."}</p>
                <div className="mt-3 flex items-center gap-2">
                  <StatusBadge
                    status={data.isPublished ? "green" : "amber"}
                    label={data.isPublished ? "Dipublikasikan (Publik)" : "Draft (Internal)"}
                  />
                  {data.slug && (
                    <span className="text-xs text-ink-muted">
                      URL: <code className="rounded bg-surface px-1.5 py-0.5 border border-border text-primary font-mono">/p/{data.slug}</code>
                    </span>
                  )}
                </div>
              </div>

              {/* Verified Competencies */}
              <div>
                <h3 className="text-sm font-semibold text-ink flex items-center gap-1.5 mb-2">
                  <Icon name="list-checks" size={16} className="text-status-green" /> Kompetensi Terverifikasi (Dari Jurnal Approved)
                </h3>
                {data.verifiedCompetencies.length === 0 ? (
                  <p className="text-xs text-ink-muted italic">Belum ada kompetensi yang disetujui melalui jurnal harian.</p>
                ) : (
                  <div className="flex flex-wrap gap-1.5">
                    {data.verifiedCompetencies.map((comp, idx) => (
                      <span
                        key={idx}
                        className="inline-flex items-center gap-1 rounded-full border border-primary/20 bg-primary-muted px-3 py-1 text-xs font-medium text-primary"
                      >
                        <Icon name="check" size={16} /> {comp}
                      </span>
                    ))}
                  </div>
                )}
              </div>

              {/* Sample Approved Journals */}
              <div>
                <h3 className="text-sm font-semibold text-ink flex items-center gap-1.5 mb-2">
                  <Icon name="file-text" size={16} className="text-primary" /> Sampel Jurnal Unggulan Pilihan Siswa
                </h3>
                {data.sampleJournals.length === 0 ? (
                  <p className="text-xs text-ink-muted italic">Siswa belum memilih sampel jurnal unggulan.</p>
                ) : (
                  <ul className="flex flex-col gap-2">
                    {data.sampleJournals.map((sample) => (
                      <li key={sample.journalEntryId} className="rounded-[var(--radius-md)] border border-border p-3 bg-surface">
                        <div className="flex items-center justify-between text-xs text-ink-muted mb-1">
                          <span>{new Date(sample.submittedAt).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" })}</span>
                          <StatusBadge status="green" label="Disetujui" />
                        </div>
                        <p className="text-sm text-ink">{sample.text}</p>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              {/* Certificate */}
              {data.certificate && (
                <div className="rounded-[var(--radius-md)] border border-status-green/30 bg-status-green-muted/20 p-4">
                  <h3 className="text-sm font-semibold text-status-green flex items-center gap-1.5">
                    <Icon name="award" size={20} /> Sertifikat Kelulusan PKL
                  </h3>
                  <p className="mt-1 text-xs text-ink-muted">
                    Kode Verifikasi Sertifikat: <strong className="text-ink font-mono">{data.certificate.certCode}</strong>
                  </p>
                  <p className="text-xs text-ink-muted">
                    Diterbitkan pada: {new Date(data.certificate.issuedAt).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" })}
                  </p>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
