"use client";

import { useEffect, useState } from "react";
import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import type { VisitDto } from "@/lib/apiTypes";

export interface VisitHistoryListProps {
  placementId: string;
  refreshKey?: number;
}

/**
 * VOK-H5-E2 §1 VisitHistoryList({placementId}) — riwayat kunjungan (tanggal, cuplikan catatan,
 * badge foto/ttd). TIDAK merender gambar foto/ttd langsung (objectKey MinIO bukan URL publik,
 * butuh presigned GET yang tak ada endpoint-nya utk kunjungan — sama pola persis
 * JournalReviewList.tsx yang cuma tampilkan "📎 N foto", bukan <img> sungguhan) — cukup badge
 * ada/tidak, konsisten dgn precedent yang sudah ada.
 *
 * `refreshKey` — VisitForm menaikkan nomor ini stlh submit sukses supaya list auto-refresh tanpa
 * lift semua state ke parent (pola paling sederhana utk 2 client component bersaudara).
 */
export function VisitHistoryList({ placementId, refreshKey }: VisitHistoryListProps) {
  const [visits, setVisits] = useState<VisitDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);

  async function load() {
    setLoading(true);
    setError(false);
    try {
      const data = await apiClient.get<VisitDto[]>(`/placements/${placementId}/visits`);
      setVisits(data);
    } catch {
      setError(true);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [placementId, refreshKey]);

  if (loading) {
    return <p className="text-sm text-ink-muted">Memuat riwayat kunjungan…</p>;
  }

  if (error) {
    return <ErrorState message="Riwayat kunjungan belum bisa dimuat." onRetry={load} />;
  }

  if (visits.length === 0) {
    return <EmptyState icon={<Icon name="flag" size={32} />} title="Belum ada kunjungan" description="Catat kunjungan pertama ke DUDI di atas." />;
  }

  return (
    <ul className="flex flex-col gap-2">
      {visits.map((v) => (
        <li key={v.id} className="rounded-[var(--radius-lg)] border border-border bg-surface p-3">
          <div className="flex items-center justify-between gap-2">
            <span className="text-sm font-medium text-ink">
              {new Date(v.date).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" })}
            </span>
            <div className="flex gap-1 text-xs">
              {v.photoKey && (
                <span title="Ada foto lokasi" className="inline-flex items-center text-ink-muted">
                  <Icon name="camera" size={16} />
                  <span className="sr-only">Ada foto lokasi</span>
                </span>
              )}
              {v.signatureKey && (
                <span title="Sudah ditandatangani" className="inline-flex items-center text-status-green">
                  <Icon name="signature" size={16} />
                  <span className="sr-only">Sudah ditandatangani</span>
                </span>
              )}
            </div>
          </div>
          <p className="mt-1 line-clamp-2 text-sm text-ink-muted">{v.notes}</p>
        </li>
      ))}
    </ul>
  );
}
