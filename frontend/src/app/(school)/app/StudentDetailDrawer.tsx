"use client";

import { useEffect, useState } from "react";
import { Button, Icon, StatusBadge } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import Link from "next/link";
import {
  JournalEntryStatus,
  ragToBadgeStatus,
  type DashboardFlaggedStudentDto,
  type JournalWithCommentsDto,
  type Paged,
  type PlacementDto,
} from "@/lib/apiTypes";

export interface StudentDetailDrawerProps {
  student: DashboardFlaggedStudentDto | null;
  periodId: string;
  onClose: () => void;
}

export function StudentDetailActions({ placementId }: { placementId: string }) {
  return (
    <nav aria-label="Tindakan siswa" className="grid grid-cols-1 gap-2 min-[360px]:grid-cols-3">
      <Link href={`/app/bimbingan/${placementId}`} className="min-h-11 border border-primary px-3 py-3 text-center text-xs font-medium text-primary focus-visible:outline-2 focus-visible:outline-focus">Lihat jurnal &amp; beri komentar</Link>
      <Link href={`/app/bimbingan/${placementId}/kunjungan`} className="min-h-11 border border-border px-3 py-3 text-center text-xs font-medium text-ink focus-visible:outline-2 focus-visible:outline-focus">Catat kunjungan</Link>
      <Link href={`/app/penilaian?placementId=${placementId}`} className="min-h-11 border border-border px-3 py-3 text-center text-xs font-medium text-ink focus-visible:outline-2 focus-visible:outline-focus">Isi penilaian</Link>
    </nav>
  );
}

function statusLabel(status: number): string {
  if (status === JournalEntryStatus.Approved) return "Disetujui";
  if (status === JournalEntryStatus.Rejected) return "Ditolak";
  return "Menunggu";
}

/**
 * VOK-H4-E2 §1 StudentDetailDrawer — ringkasan siswa (RAG+alasan, dari flagged item yang sudah
 * ada di tangan parent, TANPA fetch ulang) + riwayat jurnal terakhir (fetch on-demand saat dibuka,
 * client component krn interaktif buka/tutup). Alur 2 panggilan: (1) placements?studentId= cari
 * placementId (GAP+filter baru, lihat CompaniesAndPlacements.cs), (2) journals/for-teacher/{id}
 * (endpoint baru, lihat Dtos.cs JournalWithCommentsDto) — via apiClient (proxy, bukan fetcher server)
 * krn ini Client Component.
 */
export function StudentDetailDrawer({ student, periodId, onClose }: StudentDetailDrawerProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [entries, setEntries] = useState<JournalWithCommentsDto[]>([]);
  const [placementId, setPlacementId] = useState<string | null>(null);

  useEffect(() => {
    if (!student) return;

    let cancelled = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLoading(true);
    setError(false);
    setEntries([]);
    setPlacementId(null);

    const fetchDetail = async () => {
      try {
        const placements = await apiClient.get<Paged<PlacementDto>>(
          `/placements?periodId=${periodId}&studentId=${student.studentId}`
        );
        const placement = placements.items[0];
        if (!placement) {
          if (!cancelled) setError(true);
          return;
        }
        if (!cancelled) setPlacementId(placement.id);
        const journals = await apiClient.get<JournalWithCommentsDto[]>(`/journals/for-teacher/${placement.id}`);
        if (!cancelled) setEntries(journals.slice(0, 5));
      } catch {
        if (!cancelled) setError(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    void fetchDetail();

    return () => {
      cancelled = true;
    };
  }, [student, periodId]);

  if (!student) return null;

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <button
        type="button"
        aria-label="Tutup detail siswa"
        onClick={onClose}
        className="absolute inset-0 bg-ink/30"
      />
      <div className="relative flex h-full w-full max-w-md flex-col gap-4 overflow-y-auto border-l border-border bg-surface p-5 shadow-lg">
        <div className="flex items-start justify-between gap-3">
          <div>
            <h2 className="text-lg font-semibold text-ink">{student.name}</h2>
            <p className="text-sm text-ink-muted">{student.companyName}</p>
          </div>
          <Button variant="secondary" size="md" onClick={onClose} aria-label="Tutup">
            Tutup
          </Button>
        </div>

        <div className="flex items-center gap-2">
          <StatusBadge
            status={ragToBadgeStatus(student.rag)}
            label={student.rag === 2 ? "Merah" : student.rag === 1 ? "Kuning" : "Hijau"}
          />
          <span className="text-sm text-ink-muted">{student.reason}</span>
        </div>

        {placementId && <StudentDetailActions placementId={placementId} />}

        <div className="flex flex-col gap-2">
          <h3 className="text-sm font-semibold text-ink">Riwayat Jurnal Terakhir</h3>

          {loading && <p className="text-sm text-ink-muted">Memuat riwayat…</p>}

          {!loading && error && (
            <p className="text-sm text-status-red">Riwayat belum bisa dimuat — coba tutup dan buka lagi.</p>
          )}

          {!loading && !error && entries.length === 0 && (
            <p className="text-sm text-ink-muted">Belum ada entri jurnal untuk placement ini.</p>
          )}

          {!loading && !error && entries.length > 0 && (
            <ul className="flex flex-col gap-2">
              {entries.map(({ entry, comments }) => (
                <li key={entry.id} className="rounded-[var(--radius-md)] border border-border p-3">
                  <div className="flex items-center justify-between">
                    <span className="text-xs text-ink-muted">
                      {new Date(entry.submittedAt).toLocaleDateString("id-ID", { day: "numeric", month: "short" })}
                    </span>
                    <StatusBadge
                      status={entry.status === JournalEntryStatus.Approved ? "green" : entry.status === JournalEntryStatus.Rejected ? "red" : "amber"}
                      label={statusLabel(entry.status)}
                    />
                  </div>
                  <p className="mt-1 line-clamp-2 text-sm text-ink">{entry.text}</p>
                  {comments.length > 0 && (
                    <p className="mt-1 inline-flex items-center gap-1 text-xs text-ink-muted">
                      <Icon name="message-square-text" size={16} /> {comments.length} komentar guru
                    </p>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
