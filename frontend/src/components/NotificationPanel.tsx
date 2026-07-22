import type { NotificationDto } from "@/lib/apiTypes";

export interface NotificationPanelProps {
  items: NotificationDto[];
  onMarkRead: (id: string) => void;
  onMarkAllRead: () => void;
}

const ICON_BY_TYPE: Record<string, string> = {
  JournalApproved: "✅",
  JournalRejected: "✖",
  GhostingAlert: "🔴",
  TeacherComment: "💬",
  JournalReminder: "⏰",
  PhotoProcessingFailed: "⚠️",
  MentorWelcome: "🤝",
  PlacementWelcome: "🎉",
};

const LABEL_BY_TYPE: Record<string, string> = {
  JournalApproved: "Jurnal disetujui",
  JournalRejected: "Jurnal ditolak",
  GhostingAlert: "Siswa butuh perhatian",
  TeacherComment: "Komentar guru baru",
  JournalReminder: "Pengingat isi jurnal",
  PhotoProcessingFailed: "Foto gagal diproses",
  MentorWelcome: "Selamat datang, mentor",
  PlacementWelcome: "Placement baru",
};

/**
 * VOK-H4-E2 §3 NotificationPanel({items, onMarkRead, onMarkAllRead}) — daftar notif ber-ikon per
 * tipe. Klik item -> tandai dibaca (navigasi kontekstual per-tipe DISENGAJA TIDAK diimplementasi
 * penuh — kebanyakan tipe belum py halaman detail per-entri yang bisa dituju di shell manapun saat
 * ini; ditandai sbg gap jujur drpd link ke tempat yang tak ada, lihat DECISIONS.md).
 */
export function NotificationPanel({ items, onMarkRead, onMarkAllRead }: NotificationPanelProps) {
  const hasUnread = items.some((n) => !n.isRead);

  return (
    <div className="absolute right-0 top-full z-40 mt-2 w-80 rounded-[var(--radius-lg)] border border-border bg-surface shadow-lg">
      <div className="flex items-center justify-between border-b border-border p-3">
        <span className="text-sm font-semibold text-ink">Notifikasi</span>
        {hasUnread && (
          <button
            type="button"
            onClick={onMarkAllRead}
            className="text-xs font-medium text-primary outline-none hover:underline focus-visible:outline-2 focus-visible:outline-focus"
          >
            Tandai semua dibaca
          </button>
        )}
      </div>

      <div className="max-h-96 overflow-y-auto">
        {items.length === 0 && <p className="p-4 text-center text-sm text-ink-muted">Belum ada notifikasi.</p>}

        {items.length > 0 && (
          <ul>
            {items.map((n) => (
              <li key={n.id}>
                <button
                  type="button"
                  onClick={() => !n.isRead && onMarkRead(n.id)}
                  className={
                    "flex w-full items-start gap-2 border-b border-border p-3 text-left text-sm outline-none last:border-b-0 " +
                    "hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:-outline-offset-2 " +
                    (n.isRead ? "text-ink-muted" : "bg-primary-muted/40 font-medium text-ink")
                  }
                >
                  <span aria-hidden="true">{ICON_BY_TYPE[n.type] ?? "🔔"}</span>
                  <span className="flex flex-col gap-0.5">
                    <span>{LABEL_BY_TYPE[n.type] ?? n.type}</span>
                    <span className="text-xs text-ink-muted">
                      {new Date(n.createdAt).toLocaleString("id-ID", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                    </span>
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
