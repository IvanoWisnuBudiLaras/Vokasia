"use client";

import { useState } from "react";
import { Button, Icon } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { PortfolioDto, PublishPortfolioResult } from "@/lib/apiTypes";
import { richTextPlainText } from "@/lib/richText";
import { reconcileAppliedPortfolioMutation } from "./portfolioMutation";

interface PortfolioEditorProps {
  initialPortfolio: PortfolioDto;
}

export function PortfolioEditor({ initialPortfolio }: PortfolioEditorProps) {
  const [headline, setHeadline] = useState(initialPortfolio.headline ?? "");
  const [portfolio, setPortfolio] = useState(initialPortfolio);
  const [saving, setSaving] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [savedOnce, setSavedOnce] = useState(false);

  function clearFeedback() {
    setError(null);
    setNotice(null);
  }

  async function reconcileIfApplied(requestError: unknown, optimisticPortfolio: PortfolioDto, noticeMessage: string) {
    const result = await reconcileAppliedPortfolioMutation(
      requestError,
      optimisticPortfolio,
      () => apiClient.get<PortfolioDto>("/portfolio"),
      noticeMessage
    );
    if (!result) return false;
    setPortfolio(result.portfolio);
    setHeadline(result.portfolio.headline ?? "");
    setNotice(result.notice);
    return true;
  }

  async function saveDraft() {
    setSaving(true);
    clearFeedback();
    try {
      await apiClient.put("/portfolio", { headline: headline.trim() || null });
      const result = await apiClient.get<PortfolioDto>("/portfolio");
      setPortfolio(result);
      setHeadline(result.headline ?? "");
      setSavedOnce(true);
    } catch (err) {
      const handled = await reconcileIfApplied(err, portfolio, "Draf sudah tersimpan, tetapi status terbaru belum bisa dipastikan. Muat ulang sebelum melanjutkan.");
      if (handled) setSavedOnce(true);
      else setError(err instanceof ApiError ? err.message : "Gagal menyimpan portofolio.");
    } finally {
      setSaving(false);
    }
  }

  async function publish() {
    setPublishing(true);
    clearFeedback();
    try {
      await apiClient.put("/portfolio", { headline: headline.trim() || null });
      const saved = await apiClient.get<PortfolioDto>("/portfolio");
      setPortfolio(saved);
      setHeadline(saved.headline ?? "");
      const result = await apiClient.post<PublishPortfolioResult>("/portfolio/publish");
      setPortfolio((prev) => ({ ...prev, isPublished: true, slug: result.slug, hasUnpublishedChanges: false }));
    } catch (err) {
      const handled = await reconcileIfApplied(err, { ...portfolio, isPublished: true }, "Portofolio sudah dipublikasikan atau draf sudah tersimpan, tetapi status terbaru belum bisa dipastikan. Muat ulang sebelum melanjutkan.");
      if (!handled) setError(err instanceof ApiError ? err.message : "Gagal mempublikasikan portofolio.");
    } finally {
      setPublishing(false);
    }
  }

  async function hide() {
    setPublishing(true);
    clearFeedback();
    try {
      await apiClient.post("/portfolio/unpublish");
      setPortfolio((prev) => ({ ...prev, isPublished: false }));
    } catch (err) {
      const handled = await reconcileIfApplied(err, { ...portfolio, isPublished: false }, "Publikasi sudah disembunyikan, tetapi status terbaru belum bisa dipastikan. Muat ulang sebelum melanjutkan.");
      if (!handled) setError(err instanceof ApiError ? err.message : "Gagal menyembunyikan portofolio.");
    } finally {
      setPublishing(false);
    }
  }

  const publicUrl = portfolio.slug ? `/p/${portfolio.slug}` : null;
  const evidence = portfolio.sampleJournals;

  return (
    <div className="flex flex-col gap-5">
      <section className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-border bg-surface p-4">
        <label className="flex flex-col gap-1.5">
          <span className="text-sm font-medium text-ink">Deskripsi PKL</span>
          <p className="text-xs text-ink-muted">Opsional. Jelaskan singkat pengalaman PKL yang ingin kamu tampilkan.</p>
          <input aria-label="Deskripsi PKL" type="text" value={headline} onChange={(e) => setHeadline(e.target.value)} placeholder="Contoh: Membantu pencatatan transaksi dan laporan keuangan" maxLength={120} className="h-11 w-full rounded-[var(--radius-md)] border border-border px-3 text-base outline-none focus:outline-2 focus:outline-primary focus:outline-offset-1" />
        </label>

        <div className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface-muted p-3">
          <div>
            <h2 className="text-sm font-medium text-ink">Bukti kegiatan</h2>
            <p className="mt-1 text-xs leading-5 text-ink-muted">Bukti terverifikasi diambil otomatis dari jurnal Approved terbaru. Urutan dan isi bukti tidak dapat dipin atau diubah manual.</p>
          </div>
          {evidence.length > 0 ? (
            <ol className="flex flex-col gap-2">
              {evidence.map((sample, index) => (
                <li key={sample.journalEntryId} className="rounded-[var(--radius-md)] border border-border bg-surface p-2 text-xs text-ink"><span className="mr-2 text-ink-muted">{index + 1}.</span><span className="line-clamp-2">{richTextPlainText(sample.text)}</span></li>
              ))}
            </ol>
          ) : <p className="text-xs text-ink-muted">Belum ada jurnal Approved yang dapat ditampilkan.</p>}
        </div>

        {error && <p className="text-sm text-status-red" role="alert">{error}</p>}
        {notice && <p className="flex items-start gap-2 rounded-[var(--radius-md)] border border-status-amber bg-status-amber-bg p-3 text-sm text-status-amber" role="status"><Icon name="warning" size={20} className="mt-0.5 shrink-0" /><span>{notice}</span></p>}
        <div className="flex gap-2">
          <Button variant="primary" size="md" loading={saving} onClick={saveDraft}>Simpan Draf</Button>
          {savedOnce && !saving && <span className="inline-flex items-center gap-1 self-center text-xs text-status-green"><Icon name="check" size={16} /> Tersimpan</span>}
        </div>
      </section>

      <section className="flex flex-col gap-2 rounded-[var(--radius-lg)] border border-border bg-surface-muted p-4">
        <h2 className="text-sm font-semibold text-ink">Pratinjau isi</h2>
        <p className="text-base font-medium text-ink">{headline || <span className="text-ink-muted">(belum ada headline)</span>}</p>
        {portfolio.verifiedCompetencies.length > 0 && <p className="text-sm text-ink-muted">Kompetensi terverifikasi: {portfolio.verifiedCompetencies.join(" · ")}</p>}
        {evidence.length > 0 && <ul className="flex flex-col gap-1.5">{evidence.map((sample) => <li key={sample.journalEntryId} className="line-clamp-2 rounded-[var(--radius-md)] border border-border bg-surface p-2 text-xs text-ink-muted">{richTextPlainText(sample.text)}</li>)}</ul>}
        {portfolio.certificate && <p className="text-xs font-medium text-status-green">Sertifikat: {portfolio.certificate.certCode}</p>}
      </section>

      <section className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-border bg-surface p-4">
        <h2 className="text-sm font-semibold text-ink">Publikasi</h2>
        <p className="text-xs text-ink-muted">Portofolio yang dipublikasikan dapat dilihat siapa pun lewat tautan publik, tanpa kontak atau NISN. Kamu dapat menyembunyikannya kapan pun.</p>
        <p className="text-xs text-ink-muted">Status: <strong className="text-ink">{portfolio.isPublished ? "Dipublikasikan" : "Draf"}</strong>{portfolio.hasUnpublishedChanges && " · Ada perubahan yang belum dipublikasikan"}</p>
        {portfolio.missingPublicationRequirements.length > 0 && <p className="rounded-[var(--radius-md)] border border-status-amber bg-status-amber-bg p-3 text-sm text-status-amber" role="status">Lengkapi sebelum publikasi: {portfolio.missingPublicationRequirements.join(", ")}.</p>}
        {portfolio.isPublished && publicUrl && <p className="text-sm text-ink">Tautan aktif: <a href={publicUrl} target="_blank" rel="noreferrer" className="font-medium text-primary underline">vokasia.app{publicUrl}</a></p>}
        <div className="flex gap-2">
          {portfolio.isPublished && portfolio.hasUnpublishedChanges && <Button variant="primary" size="md" loading={publishing} onClick={publish}>Perbarui publikasi</Button>}
          {portfolio.isPublished ? <Button variant="secondary" size="md" loading={publishing} onClick={hide}>Sembunyikan portofolio</Button> : <Button variant="primary" size="md" loading={publishing} onClick={publish}>Publikasikan</Button>}
        </div>
      </section>
    </div>
  );
}
