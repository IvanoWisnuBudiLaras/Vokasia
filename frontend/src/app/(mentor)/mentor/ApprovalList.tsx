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
    setBanner(null);
    markBusy(id, true);
    setGroups((prev) => removeIds(prev, new Set([id])));

    try {
      await apiClient.post(`/journals/${id}/approve`, { note: null });
      unselect(id);
    } catch (err) {
      setGroups(snapshot);
      setBanner({ type: "error", message: err instanceof ApiError ? err.message : "Gagal menyetujui jurnal." });
    } finally {
      markBusy(id, false);
    }
  }

  async function submitReject(id: string, reason: string) {
    const snapshot = groups;
    setBanner(null);
    markBusy(id, true);
    setGroups((prev) => removeIds(prev, new Set([id])));

    try {
      await apiClient.post(`/journals/${id}/reject`, { reason });
      setRejectTarget(null);
      unselect(id);
    } catch (err) {
      setGroups(snapshot); // dialog sengaja TETAP terbuka supaya mentor bisa coba lagi
      setBanner({ type: "error", message: err instanceof ApiError ? err.message : "Gagal menolak jurnal." });
    } finally {
      markBusy(id, false);
    }
  }

  async function approveBatch() {
    if (selectedIds.size === 0) return;
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
      setBanner({ type: "error", message: err instanceof ApiError ? err.message : "Gagal memproses batch approve." });
    } finally {
      setBatchBusy(false);
    }
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
        onApprove={approveBatch}
      />

      {banner && (
        <p role="status" className={banner.type === "error" ? "text-sm text-status-red" : "text-sm text-status-green"}>
          {banner.message}
        </p>
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
    </div>
  );
}
