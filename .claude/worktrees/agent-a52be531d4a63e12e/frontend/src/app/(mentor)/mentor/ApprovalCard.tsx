"use client";

import { Button } from "@/components/ui";
import { cn } from "@/lib/cn";
import type { JournalDto } from "@/lib/apiTypes";

interface ApprovalCardProps {
  journal: JournalDto;
  studentName: string;
  expanded: boolean;
  selected: boolean;
  busy: boolean;
  onToggleExpand: () => void;
  onToggleSelect: () => void;
  onApprove: () => void;
  onReject: () => void;
}

/**
 * VOK-H3-E2 §2 ApprovalCard({journal, expanded}) — ringkas per jurnal (nama, cuplikan teks);
 * expand -> teks penuh.
 *
 * [GAP dicatat, bukan diam-diam]: wireframe W2 minta "sekolah" + "foto thumbnail" + "⚠ tanda hari
 * kosong" per kartu. Tak satu pun tersedia dari data GetPendingApprovals (backend H3-E1):
 * PendingGroupDto tak punya field sekolah/nama-tenant sama sekali; JournalDto.photos dari endpoint
 * INI SELALU [] (lihat GetPendingApprovals di JournalEndpoints.cs: "ToDto(x.Entry, [], [])" -
 * sengaja dikosongkan krn "ringkasan approval TIDAK perlu foto/kompetensi penuh"); dan bahkan bila
 * backend diperkaya mengembalikan Photos asli, belum ada mekanisme presigned READ URL sama sekali
 * (hanya presigned PUT utk upload) - jadi thumbnail nyata butuh 2 perubahan backend, bukan 1. Kartu
 * di bawah menampilkan badge jumlah foto HANYA kalau array-nya terisi (kode benar utk masa depan),
 * dan TIDAK menampilkan "sekolah"/"⚠ hari kosong" sama sekali (drpd mengarang data kosong/statis).
 * Di luar wilayah ticket ini (`frontend/` saja) - dicatat DECISIONS.md D26 utk H3+ berikutnya.
 */
export function ApprovalCard({
  journal,
  studentName,
  expanded,
  selected,
  busy,
  onToggleExpand,
  onToggleSelect,
  onApprove,
  onReject,
}: ApprovalCardProps) {
  return (
    <div className={cn("flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-3", selected && "border-primary bg-primary-muted/30")}>
      <div className="flex items-start gap-2">
        <input
          type="checkbox"
          checked={selected}
          onChange={onToggleSelect}
          disabled={busy}
          aria-label={`Pilih jurnal ${studentName}`}
          className="mt-1 h-5 w-5 shrink-0 accent-[var(--color-primary)]"
        />
        <button type="button" onClick={onToggleExpand} className="flex-1 text-left">
          <p className="text-sm font-medium text-ink">{studentName}</p>
          <p className={cn("text-sm text-ink-muted", !expanded && "line-clamp-2")}>{journal.text}</p>
          {journal.photos.length > 0 && (
            <span className="mt-1 inline-block text-xs text-ink-muted">📎 {journal.photos.length} foto</span>
          )}
        </button>
        <span className="mt-1 text-ink-muted" aria-hidden="true">
          {expanded ? "▲" : "▼"}
        </span>
      </div>

      {expanded && (
        <div className="flex gap-2 border-t border-border pt-2">
          <Button variant="secondary" size="md" className="flex-1" onClick={onReject} disabled={busy}>
            ✖ Tolak
          </Button>
          <Button size="md" className="flex-1" onClick={onApprove} disabled={busy} loading={busy}>
            ✔ Approve
          </Button>
        </div>
      )}
    </div>
  );
}
