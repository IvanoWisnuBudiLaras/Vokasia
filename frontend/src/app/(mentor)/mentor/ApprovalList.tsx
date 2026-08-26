"use client";

import { useMemo, useState } from "react";
import { apiClient, ApiError } from "@/lib/apiClient";
import { EmptyState, Icon } from "@/components/ui";
import type { BatchResult, JournalDto, PendingGroupDto } from "@/lib/apiTypes";
import { ApprovalCard } from "./ApprovalCard";
import { RejectDialog } from "./RejectDialog";
import { SelectAllBar } from "./SelectAllBar";

interface ApprovalListProps {
  initialGroups: PendingGroupDto[];
}

interface FlatEntry {
  journal: JournalDto;
  studentName: string;
}

function flatten(groups: PendingGroupDto[]): FlatEntry[] {
  return groups.flatMap((g) => g.entries.map((e) => ({ journal: e, studentName: g.studentFullName })));
}

function removeIds(groups: PendingGroupDto[], ids: Set<string>): PendingGroupDto[] {
  return groups.map((g) => ({ ...g, entries: g.entries.filter((e) => !ids.has(e.id)) })).filter((g) => g.entries.length > 0);
}

/**
 * VOK-H3-E2 §2 ApprovalList({groups}) — daftar FLAT per-jurnal (bukan grup collapsible per siswa):
 * ticket menulis tanda tangan `ApprovalCard({journal, expanded})` — 1 kartu per JURNAL dgn expand
 * individual, bukan 1 kartu per SISWA. Nama siswa tetap tampil di tiap kartu (lihat ApprovalCard
 * komentar gap: tanpa nama sekolah, tanpa foto thumbnail nyata, tanpa tanda "hari kosong" - data
 * tak tersedia dari GetPendingApprovals).
 *
 * Approve/reject/batch approve SEMUA optimistic+rollback (AC eksplisit ticket): entry dihapus dari
 * layar SEKETIKA saat aksi ditekan (bukan menunggu response API dulu), dikembalikan (rollback) +
 * banner error kalau API gagal. Batch approve: rollback SEBAGIAN kalau BatchResult.failed tidak
 * kosong (entry yg gagal dikembalikan ke layar, yg sukses tetap hilang) - beda dari rollback TOTAL
 * saat request itu sendiri throw (network/500 dsb, bukan kegagalan per-item terstruktur).
 */
export function ApprovalList({ initialGroups }: ApprovalListProps) {
  const [groups, setGroups] = useState(initialGroups);
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [busyIds, setBusyIds] = useState<Set<string>>(new Set());
  const [batchBusy, setBatchBusy] = useState(false);
  const [banner, setBanner] = useState<{ type: "error" | "info"; message: string } | null>(null);
  const [rejectTarget, setRejectTarget] = useState<FlatEntry | null>(null);
  const [confirmBatch, setConfirmBatch] = useState(false);
  const [nextReviewId, setNextReviewId] = useState<string | null>(null);

  const flat = useMemo(() => flatten(groups), [groups]);
  const total = flat.length;

  function toggleExpand(id: string) {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function toggleSelect(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function markBusy(id: string, busy: boolean) {
    setBusyIds((prev) => {
      const next = new Set(prev);
      if (busy) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  function unselect(id: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      next.delete(id);
      return next;
    });
  }

  async function approveOne(id: string) {
    const snapshot = groups;
    const nextId = flatten(snapshot).findIndex((item) => item.journal.id === id);
    const nextReview = flatten(snapshot)[nextId + 1]?.journal.id ?? null;
    setBanner(null);
    markBusy(id, true);
    setGroups((prev) => removeIds(prev, new Set([id])));

    try {
      await apiClient.post(`/journals/${id}/approve`, { note: null });
      unselect(id);
      setNextReviewId(nextReview);
      setBanner({ type: "info", message: "Jurnal disetujui." });
    } catch (err) {
      setGroups(snapshot);
      setBanner({ type: "error", message: err instanceof ApiError ? err.message : "Gagal menyetujui jurnal." });
    } finally {
      markBusy(id, false);
    }
  }

  async function submitReject(id: string, reason: string) {
    const snapshot = groups;
    const entries = flatten(snapshot);
    const currentIndex = entries.findIndex((item) => item.journal.id === id);
    const nextReview = entries[currentIndex + 1]?.journal.id ?? null;
    setBanner(null);
    markBusy(id, true);
    setGroups((prev) => removeIds(prev, new Set([id])));

    try {
      await apiClient.post(`/journals/${id}/reject`, { reason });
      setRejectTarget(null);
      unselect(id);
      setNextReviewId(nextReview);
      setBanner({ type: "info", message: "Permintaan revisi dikirim." });
    } catch (err) {
      setGroups(snapshot); // dialog sengaja TETAP terbuka supaya mentor bisa coba lagi
      setBanner({ type: "error", message: err instanceof ApiError ? err.message : "Gagal menolak jurnal." });
    } finally {
      markBusy(id, false);
    }
  }

  async function approveBatch() {
    if (selectedIds.size === 0) return;
    setConfirmBatch(false);
    const snapshot = groups;
    const idsToApprove = new Set(selectedIds);
    setBanner(null);
    setBatchBusy(true);
    setGroups((prev) => removeIds(prev, idsToApprove));
    setSelectedIds(new Set());

    try {
      const result = await apiClient.post<BatchResult>("/journals/batch-approve", { ids: Array.from(idsToApprove) });

      if (result.failed.length > 0) {
        const failedIds = new Set(result.failed.map((f) => f.id));
        const restored = snapshot
          .map((g) => ({ ...g, entries: g.entries.filter((e) => failedIds.has(e.id)) }))
          .filter((g) => g.entries.length > 0);

        setGroups((prev) => {
          const merged = prev.map((g) => ({ ...g, entries: [...g.entries] }));
          for (const rg of restored) {
            const existing = merged.find((m) => m.studentId === rg.studentId);
            if (existing) existing.entries.push(...rg.entries);
            else merged.push(rg);
          }
          return merged;
        });

        setBanner({
          type: "error",
          message: `${result.approved.length} jurnal disetujui, ${result.failed.length} gagal (${result.failed[0].reason}).`,
        });
      } else {
        setBanner({ type: "info", message: `${result.approved.length} jurnal disetujui.` });
      }
    } catch (err) {
      setGroups(snapshot);
      setSelectedIds(idsToApprove);
      setBanner({ type: "error", message: err instanceof ApiError ? err.message : "Gagal menyetujui jurnal yang dipilih." });
    } finally {
      setBatchBusy(false);
    }
  }

  function requestBatchApproval() {
    if (selectedIds.size > 0) setConfirmBatch(true);
  }

  function reviewNext() {
    if (!nextReviewId) return;
    setExpandedIds((previous) => new Set(previous).add(nextReviewId));
    setNextReviewId(null);
    requestAnimationFrame(() => document.getElementById(`approval-${nextReviewId}`)?.scrollIntoView({ block: "center", behavior: "smooth" }));
  }

  if (total === 0) {
    return (
      <EmptyState
        icon={<Icon name="check" size={32} />}
        title="Belum ada jurnal untuk ditinjau"
        description="Semua jurnal siswa bimbinganmu sudah diproses. Jurnal baru akan muncul di sini begitu dikirim."
      />
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <SelectAllBar
        selectedCount={selectedIds.size}
        total={total}
        busy={batchBusy}
        onSelectAll={() => setSelectedIds(new Set(flat.map((f) => f.journal.id)))}
        onClear={() => setSelectedIds(new Set())}
        onApprove={requestBatchApproval}
      />

      {banner && (
        <div className="flex flex-wrap items-center gap-3" role="status">
          <p className={banner.type === "error" ? "text-sm text-status-red" : "text-sm text-status-green"}>{banner.message}</p>
          {nextReviewId && <button type="button" onClick={reviewNext} className="min-h-[var(--tap-min)] text-sm font-semibold text-primary underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2">Review berikutnya</button>}
        </div>
      )}

      <ul className="flex flex-col gap-2">
        {flat.map(({ journal, studentName }) => (
          <li key={journal.id}>
            <ApprovalCard
              journal={journal}
              studentName={studentName}
              expanded={expandedIds.has(journal.id)}
              selected={selectedIds.has(journal.id)}
              busy={busyIds.has(journal.id) || batchBusy}
              onToggleExpand={() => toggleExpand(journal.id)}
              onToggleSelect={() => toggleSelect(journal.id)}
              onApprove={() => approveOne(journal.id)}
              onReject={() => setRejectTarget({ journal, studentName })}
            />
          </li>
        ))}
      </ul>

      {rejectTarget && (
        <RejectDialog
          studentName={rejectTarget.studentName}
          busy={busyIds.has(rejectTarget.journal.id)}
          onClose={() => setRejectTarget(null)}
          onSubmit={(reason) => submitReject(rejectTarget.journal.id, reason)}
        />
      )}

      {confirmBatch && (
        <div className="fixed inset-0 z-50 flex items-end justify-center bg-ink/40 p-4 sm:items-center" role="presentation" onKeyDown={(event) => { if (event.key === "Escape") setConfirmBatch(false); }}>
          <div role="alertdialog" aria-modal="true" aria-labelledby="batch-approval-title" aria-describedby="batch-approval-description" className="w-full max-w-md border border-border bg-surface p-5 shadow-lg">
            <h2 id="batch-approval-title" className="text-lg font-semibold text-ink">Setujui jurnal terpilih?</h2>
            <p id="batch-approval-description" className="mt-2 text-sm text-ink-muted">Setujui {selectedIds.size} jurnal dari {new Set(flat.filter((entry) => selectedIds.has(entry.journal.id)).map((entry) => entry.studentName)).size} siswa?</p>
            <div className="mt-5 flex flex-col gap-2 min-[24rem]:flex-row min-[24rem]:justify-end">
              <button type="button" autoFocus onClick={() => setConfirmBatch(false)} className="min-h-[var(--tap-min)] border border-border px-4 text-sm font-semibold text-ink outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus">Batal</button>
              <button type="button" onClick={approveBatch} className="min-h-[var(--tap-min)] bg-primary px-4 text-sm font-semibold text-primary-ink outline-none hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-focus">Setujui jurnal</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
