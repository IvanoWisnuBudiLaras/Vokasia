"use client";

import { useState } from "react";
import { Button } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { JournalDto, PortfolioDto, PublishPortfolioResult } from "@/lib/apiTypes";
import { SamplePicker } from "./SamplePicker";

interface PortfolioEditorProps {
  initialPortfolio: PortfolioDto;
  approvedJournals: JournalDto[];
}

const MAX_SAMPLES = 6;

/**
 * VOK-H6-E2 §3 student/portofolio — editor: headline, SamplePicker (maks 6 sampel dari jurnal
 * Approved), toggle Publikasikan/Unpublish dgn consent copy eksplisit (AC literal: "dapat dilihat
 * siapa pun, tanpa kontak/NISN"). Preview di bawah = best-effort ringkas dari field yg TERSEDIA di
 * PortfolioDto privat (headline, kompetensi, teks sampel, sertifikat) — DTO ini SENGAJA tidak
 * memuat nama/sekolah/DUDI/durasi (itu hanya dihitung backend saat GetPublicPortfolio, dari join
 * placement terbaru), jadi preview di sini bukan cermin piksel-demi-piksel /p/[slug] — tetap
 * cukup utk siswa menilai isi kurasi (headline+kompetensi+sampel+sertifikat) sebelum publikasi.
 * Menambah field identitas ke PortfolioDto backend hanya utk preview di luar wilayah ticket ini
 * (`frontend/` saja).
 */
export function PortfolioEditor({ initialPortfolio, approvedJournals }: PortfolioEditorProps) {
  const [headline, setHeadline] = useState(initialPortfolio.headline ?? "");
  const [selectedIds, setSelectedIds] = useState<string[]>(initialPortfolio.sampleJournals.map((s) => s.journalEntryId));
  const [portfolio, setPortfolio] = useState(initialPortfolio);
  const [saving, setSaving] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedOnce, setSavedOnce] = useState(false);

  const selectedJournals = approvedJournals.filter((j) => selectedIds.includes(j.id));

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      await apiClient.put("/portfolio", { headline: headline.trim() || null, sampleJournalIds: selectedIds });
      setSavedOnce(true);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menyimpan portofolio.");
    } finally {
      setSaving(false);
    }
  }

  async function handlePublish() {
    setPublishing(true);
    setError(null);
    try {
      // AC: publikasi harus menyimpan draf terbaru dulu (headline/sampel bisa saja belum di-PUT).
      await apiClient.put("/portfolio", { headline: headline.trim() || null, sampleJournalIds: selectedIds });
      const result = await apiClient.post<PublishPortfolioResult>("/portfolio/publish");
      setPortfolio((prev) => ({ ...prev, isPublished: true, slug: result.slug }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal publikasikan portofolio.");
    } finally {
      setPublishing(false);
    }
  }

  async function handleUnpublish() {
    setPublishing(true);
    setError(null);
    try {
      await apiClient.post("/portfolio/unpublish");
      setPortfolio((prev) => ({ ...prev, isPublished: false }));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menonaktifkan portofolio.");
    } finally {
      setPublishing(false);
    }
  }

  const publicUrl = portfolio.slug ? `/p/${portfolio.slug}` : null;

  return (
    <div className="flex flex-col gap-5">
      <section className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-border bg-surface p-4">
        <label className="flex flex-col gap-1.5">
          <span className="text-sm font-medium text-ink">Headline</span>
          <input
            type="text"
            value={headline}
            onChange={(e) => setHeadline(e.target.value)}
            placeholder="mis. Siswa PKL Rekayasa Perangkat Lunak — antusias di bidang web development"
            maxLength={140}
            className="h-11 w-full rounded-[var(--radius-md)] border border-border px-3 text-base outline-none focus:outline-2 focus:outline-primary focus:outline-offset-1"
          />
        </label>

        <SamplePicker approvedJournals={approvedJournals} selected={selectedIds} max={MAX_SAMPLES} onChange={setSelectedIds} />

        {error && <p className="text-sm text-status-red">{error}</p>}

        <div className="flex gap-2">
          <Button variant="primary" size="md" loading={saving} onClick={handleSave}>
            Simpan Draf
          </Button>
          {savedOnce && !saving && <span className="self-center text-xs text-status-green">✓ Tersimpan</span>}
        </div>
      </section>

      <section className="flex flex-col gap-2 rounded-[var(--radius-lg)] border border-border bg-surface-muted p-4">
        <h2 className="text-sm font-semibold text-ink">Pratinjau</h2>
        <p className="text-base font-medium text-ink">{headline || <span className="text-ink-muted">(belum ada headline)</span>}</p>
        {portfolio.verifiedCompetencies.length > 0 && (
          <ul className="flex flex-wrap gap-1.5">
            {portfolio.verifiedCompetencies.map((c) => (
              <li key={c} className="rounded-full bg-primary-muted px-2.5 py-0.5 text-xs font-medium text-ink">
                {c}
              </li>
            ))}
          </ul>
        )}
        {selectedJournals.length > 0 && (
          <ul className="flex flex-col gap-1.5">
            {selectedJournals.map((j) => (
              <li key={j.id} className="line-clamp-2 rounded-[var(--radius-md)] border border-border bg-surface p-2 text-xs text-ink-muted">
                {j.text}
              </li>
            ))}
          </ul>
        )}
        {portfolio.certificate && (
          <p className="text-xs font-medium text-status-green">🏆 Sertifikat: {portfolio.certificate.certCode}</p>
        )}
      </section>

      <section className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-border bg-surface p-4">
        <h2 className="text-sm font-semibold text-ink">Publikasi</h2>
        <p className="text-xs text-ink-muted">
          Portofolio yang dipublikasikan <strong>dapat dilihat siapa pun</strong> lewat tautan publik — <strong>tanpa</strong>{" "}
          kontak atau NISN kamu. Kamu bisa menonaktifkan (Unpublish) kapan pun.
        </p>

        {portfolio.isPublished && publicUrl && (
          <p className="text-sm text-ink">
            Tautan aktif:{" "}
            <a href={publicUrl} target="_blank" rel="noreferrer" className="font-medium text-primary underline">
              vokasia.app{publicUrl}
            </a>
          </p>
        )}

        <div className="flex gap-2">
          {portfolio.isPublished ? (
            <Button variant="secondary" size="md" loading={publishing} onClick={handleUnpublish}>
              Unpublish
            </Button>
          ) : (
            <Button variant="primary" size="md" loading={publishing} onClick={handlePublish}>
              Publikasikan
            </Button>
          )}
        </div>
      </section>
    </div>
  );
}
