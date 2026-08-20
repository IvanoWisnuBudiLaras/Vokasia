"use client";

import { Icon } from "@/components/ui";
import { MaterialButton } from "@/components/ui/MaterialButton";
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
        <label
          className={cn(
            "flex min-h-[var(--tap-min)] min-w-[var(--tap-min)] shrink-0 items-start justify-center rounded-[var(--radius-sm)] pt-1",
            busy ? "cursor-not-allowed opacity-50" : "cursor-pointer active:bg-primary-muted"
          )}
        >
          <input
            type="checkbox"
            checked={selected}
            onChange={onToggleSelect}
            disabled={busy}
            aria-label={`Pilih jurnal ${studentName}`}
            className="h-5 w-5 accent-[var(--color-primary)] disabled:cursor-not-allowed"
          />
        </label>
        <button
          type="button"
          onClick={onToggleExpand}
          aria-expanded={expanded}
          aria-label={`${expanded ? "Tutup" : "Buka"} jurnal ${studentName}`}
          className="min-h-[var(--tap-min)] flex-1 rounded-[var(--radius-sm)] px-1 text-left outline-none transition-[color,background-color,border-color] hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
        >
          <p className="text-sm font-medium text-ink">{studentName}</p>
          <p className={cn("text-sm text-ink-muted", !expanded && "line-clamp-2")}>{journal.text}</p>
          {journal.photos.length > 0 && (
            <span className="mt-1 inline-flex items-center gap-1 text-xs text-ink-muted">
              <Icon name="image" size={16} /> {journal.photos.length} foto
            </span>
          )}
        </button>
        <span className="mt-3 text-ink-muted" aria-hidden="true">
          <Icon name={expanded ? "chevron-up" : "chevron-down"} size={16} />
        </span>
      </div>

      {expanded && (
        <div className="flex flex-col gap-2 border-t border-border pt-2 min-[24rem]:flex-row">
          <MaterialButton
            className="w-full whitespace-nowrap border-status-red text-status-red min-[24rem]:w-auto min-[24rem]:flex-1"
            onClick={onReject}
            disabled={busy}
          >
            <Icon name="x" size={16} /> Tolak dan kirim alasan
          </MaterialButton>
          <MaterialButton
            className="w-full whitespace-nowrap bg-primary text-primary-ink min-[24rem]:w-auto min-[24rem]:flex-1"
            onClick={onApprove}
            disabled={busy}
            aria-busy={busy}
          >
            <Icon name="check" size={16} /> Setujui jurnal
          </MaterialButton>
        </div>
      )}
    </div>
  );
}
