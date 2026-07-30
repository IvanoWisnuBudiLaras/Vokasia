"use client";

import { useEffect, useState } from "react";
import { Button, EmptyState, ErrorState, Icon, StatusBadge, Textarea } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { JournalEntryStatus, type JournalWithCommentsDto } from "@/lib/apiTypes";

export interface JournalReviewListProps {
  placementId: string;
}

function statusBadge(status: number) {
  if (status === JournalEntryStatus.Approved) return <StatusBadge status="green" label="Disetujui" />;
  if (status === JournalEntryStatus.Rejected) return <StatusBadge status="red" label="Ditolak" />;
  return <StatusBadge status="amber" label="Menunggu" />;
}

/**
 * VOK-H4-E2 §2 JournalReviewList({placementId}) — baca jurnal siswa (GET /journals/for-teacher/
 * {placementId}, endpoint baru H4-E2) + AddTeacherComment inline (FR-JRN-05); komentar tampil
 * kronologis (API sudah urutkan by CreatedAt ascending, lihat backend). Client component penuh
 * (fetch on mount by placementId + submit komentar) — dipanggil dari bimbingan/page.tsx (Server
 * Component) yang cuma meneruskan placementId terpilih dari ?placementId= URL query.
 */
export function JournalReviewList({ placementId }: JournalReviewListProps) {
  const [items, setItems] = useState<JournalWithCommentsDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [submittingId, setSubmittingId] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    setError(false);
    try {
      const data = await apiClient.get<JournalWithCommentsDto[]>(`/journals/for-teacher/${placementId}`);
      setItems(data);
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
  }, [placementId]);

  async function submitComment(entryId: string) {
    const text = (drafts[entryId] ?? "").trim();
    if (text.length === 0) return;

    setSubmittingId(entryId);
    setSubmitError(null);
    try {
      await apiClient.post(`/journals/${entryId}/comments`, { text });
      setDrafts((d) => ({ ...d, [entryId]: "" }));
      await load(); // refresh supaya komentar baru + urutan kronologis dari server (bukan optimistic tebakan urutan).
    } catch (err) {
      setSubmitError(err instanceof ApiError ? err.message : "Komentar gagal dikirim. Coba lagi.");
    } finally {
      setSubmittingId(null);
    }
  }

  if (loading) {
    return <p className="text-sm text-ink-muted">Memuat jurnal…</p>;
  }

  if (error) {
    return <ErrorState message="Jurnal siswa ini belum bisa dimuat." onRetry={load} />;
  }

  if (items.length === 0) {
    return <EmptyState icon={<Icon name="notebook-pen" size={32} />} title="Belum ada jurnal" description="Siswa ini belum mengirim jurnal apa pun." />;
  }

  return (
    <div className="flex flex-col gap-3">
      {submitError && <p className="text-sm text-status-red">{submitError}</p>}

      <ul className="flex flex-col gap-3">
        {items.map(({ entry, comments }) => (
          <li key={entry.id} className="rounded-[var(--radius-lg)] border border-border bg-surface p-4">
            <div className="flex items-center justify-between">
              <span className="text-xs text-ink-muted">
                {new Date(entry.submittedAt).toLocaleDateString("id-ID", { day: "numeric", month: "long", year: "numeric" })}
              </span>
              {statusBadge(entry.status)}
            </div>
            <p className="mt-2 text-sm text-ink">{entry.text}</p>
            {entry.photos.length > 0 && (
              <p className="mt-1 inline-flex items-center gap-1 text-xs text-ink-muted">
                <Icon name="image" size={16} /> {entry.photos.length} foto
              </p>
            )}

            {comments.length > 0 && (
              <ul className="mt-3 flex flex-col gap-2 border-t border-border pt-3">
                {comments.map((c) => (
                  <li key={c.id} className="rounded-[var(--radius-sm)] bg-surface-muted p-2 text-sm text-ink">
                    <p>{c.text}</p>
                    <p className="mt-1 text-xs text-ink-muted">
                      {new Date(c.createdAt).toLocaleDateString("id-ID", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                    </p>
                  </li>
                ))}
              </ul>
            )}

            <div className="mt-3 flex flex-col gap-2 border-t border-border pt-3">
              <Textarea
                label="Tambah komentar"
                maxLength={500}
                showCounter={false}
                value={drafts[entry.id] ?? ""}
                onChange={(e) => setDrafts((d) => ({ ...d, [entry.id]: e.target.value }))}
                className="min-h-16"
              />
              <Button
                variant="secondary"
                size="md"
                className="self-end"
                loading={submittingId === entry.id}
                disabled={(drafts[entry.id] ?? "").trim().length === 0}
                onClick={() => submitComment(entry.id)}
              >
                Kirim Komentar
              </Button>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
