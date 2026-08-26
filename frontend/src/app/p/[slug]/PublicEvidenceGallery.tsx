"use client";

import Image from "next/image";
import { useEffect, useState } from "react";
import type { PublicPortfolioEvidenceDto } from "@/lib/apiTypes";
import { ShareFileButton } from "./ShareFileButton";

const INITIAL_VISIBLE = 4;

function dateLabel(value: string) {
  return new Intl.DateTimeFormat("id-ID", { dateStyle: "medium" }).format(new Date(value));
}

export function PublicEvidenceGallery({ evidence, studentName }: { evidence: PublicPortfolioEvidenceDto[]; studentName: string }) {
  const [showAll, setShowAll] = useState(false);
  const [selected, setSelected] = useState<number | null>(null);
  const visible = showAll ? evidence : evidence.slice(0, INITIAL_VISIBLE);
  const selectedEvidence = selected === null ? null : evidence[selected];

  useEffect(() => {
    if (selected === null) return;
    function close(event: KeyboardEvent) {
      if (event.key === "Escape") setSelected(null);
    }
    document.addEventListener("keydown", close);
    return () => document.removeEventListener("keydown", close);
  }, [selected]);

  if (evidence.length === 0) return null;

  return (
    <section aria-labelledby="bukti-kegiatan" className="flex flex-col gap-4">
      <div className="flex items-end justify-between gap-4 border-b border-border pb-3">
        <div>
          <h2 id="bukti-kegiatan" className="text-xl font-semibold text-ink">Bukti kegiatan</h2>
          <p className="mt-1 text-sm text-ink-muted">Jurnal Approved, terbaru lebih dulu.</p>
        </div>
        {evidence.length > INITIAL_VISIBLE && <button type="button" className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] px-2 text-sm font-medium text-primary underline focus-visible:outline-2 focus-visible:outline-focus" onClick={() => setShowAll((value) => !value)}>{showAll ? "Tampilkan lebih sedikit" : "Lihat semua"}</button>}
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {visible.map((item, index) => (
          <button type="button" key={`${item.submittedAt}-${index}`} className="group flex min-h-[var(--tap-min)] flex-col overflow-hidden rounded-[var(--radius-md)] border border-border bg-surface text-left outline-none focus-visible:outline-2 focus-visible:outline-focus" onClick={() => setSelected(evidence.indexOf(item))}>
            {item.mediaUrl ? <Image src={item.mediaUrl} alt={`Bukti kegiatan ${studentName}, ${item.context}`} width={720} height={480} className="aspect-[3/2] w-full object-cover transition-transform group-hover:scale-[1.02]" /> : <div className="flex aspect-[3/2] items-center justify-center bg-surface-muted px-4 text-sm text-ink-muted">Bukti teks</div>}
            <span className="flex flex-col gap-1 p-3"><strong className="line-clamp-2 text-sm font-medium text-ink">{item.context}</strong><time className="text-xs text-ink-muted" dateTime={item.submittedAt}>{dateLabel(item.submittedAt)}</time></span>
          </button>
        ))}
      </div>

      {selectedEvidence && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink/70 p-4" role="dialog" aria-modal="true" aria-labelledby="evidence-detail-title" onClick={() => setSelected(null)}>
          <div className="relative max-h-[90vh] w-full max-w-3xl overflow-auto rounded-[var(--radius-lg)] bg-surface p-4 shadow-xl" onClick={(event) => event.stopPropagation()}>
            <button type="button" aria-label="Tutup detail bukti" className="absolute right-3 top-3 z-10 min-h-[var(--tap-min)] rounded-full bg-surface px-3 text-xl text-ink shadow-sm focus-visible:outline-2 focus-visible:outline-focus" onClick={() => setSelected(null)}>×</button>
            <h2 id="evidence-detail-title" className="pr-12 text-lg font-semibold text-ink">{selectedEvidence.context}</h2>
            <time className="mt-1 block text-sm text-ink-muted" dateTime={selectedEvidence.submittedAt}>{dateLabel(selectedEvidence.submittedAt)}</time>
            {selectedEvidence.mediaUrl && <><Image src={selectedEvidence.mediaUrl} alt={`Bukti kegiatan ${studentName}, ${selectedEvidence.context}`} width={1200} height={800} className="mt-4 max-h-[70vh] w-full object-contain" /><div className="mt-4 flex justify-end"><ShareFileButton url={selectedEvidence.mediaUrl} filename={`bukti-kegiatan-${selected}.jpg`} label="Bagikan gambar" title={`Bukti kegiatan ${studentName}`} /></div></>}
          </div>
        </div>
      )}
    </section>
  );
}
