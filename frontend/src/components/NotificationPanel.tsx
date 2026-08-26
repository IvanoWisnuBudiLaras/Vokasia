import type { NotificationDto } from "@/lib/apiTypes";
import { Icon, type IconName } from "@/components/ui";

export interface NotificationPanelProps {
  items: NotificationDto[];
  onMarkRead: (id: string) => void;
  onMarkAllRead: () => void;
  align?: "left" | "right";
}

const ICON_BY_TYPE: Record<string, IconName> = {
  JournalApproved: "check",
  JournalRejected: "x",
  GhostingAlert: "warning",
  TeacherComment: "message-square-text",
  JournalReminder: "calendar-days",
  PhotoProcessingFailed: "warning",
  MentorWelcome: "briefcase-business",
  PlacementWelcome: "briefcase-business",
  AssessmentPhaseOpened: "file-pen-line",
  ExportReady: "package",
};

const LABEL_BY_TYPE: Record<string, string> = {
  JournalApproved: "Jurnal disetujui",
  JournalRejected: "Perlu revisi",
  GhostingAlert: "Siswa butuh perhatian",
  TeacherComment: "Komentar guru baru",
  JournalReminder: "Pengingat isi jurnal",
  PhotoProcessingFailed: "Foto gagal diproses",
  MentorWelcome: "Selamat datang, mentor",
  PlacementWelcome: "Placement baru",
  AssessmentPhaseOpened: "Fase penilaian dibuka",
  ExportReady: "Export rekap nilai siap",
};

/**
 * VOK-H5-E2 §3 — ExportReady SATU-SATUNYA tipe yang butuh link unduh nyata (AC ExportButton:
 * "notif ExportReady -> link unduh"), sisanya SENGAJA tetap tanpa navigasi kontekstual (lihat
 * doc-comment komponen ini persis di atas — kebanyakan tipe lain belum py halaman detail). Parse
 * `payloadJson` defensif (try/catch) — bentuknya `{Id, downloadUrl, expiresAt}` (PascalCase+
 * camelCase CAMPUR, cermin persis `ExportRequestedConsumer.cs` yang serialize anonymous object
 * TANPA opsi naming policy - lihat baris `new { exportRequest.Id, downloadUrl, expiresAt }`).
 */
function extractDownloadUrl(payloadJson: string): string | null {
  try {
    const parsed = JSON.parse(payloadJson) as { downloadUrl?: string };
    return typeof parsed.downloadUrl === "string" ? parsed.downloadUrl : null;
  } catch {
    return null;
  }
}

function extractJournalId(payloadJson: string): string | null {
  try {
    const parsed = JSON.parse(payloadJson) as Record<string, unknown>;
    for (const key of ["journalId", "JournalId", "entryId", "EntryId"]) {
      if (typeof parsed[key] === "string" && parsed[key].length > 0) return parsed[key];
    }
  } catch {
    // Payload not meant for navigation; leave the notification as a normal item.
  }
  return null;
}

/**
 * VOK-H4-E2 §3 NotificationPanel({items, onMarkRead, onMarkAllRead}) — daftar notif ber-ikon per
 * tipe. Klik item -> tandai dibaca (navigasi kontekstual per-tipe DISENGAJA TIDAK diimplementasi
 * penuh — kebanyakan tipe belum py halaman detail per-entri yang bisa dituju di shell manapun saat
 * ini; ditandai sbg gap jujur drpd link ke tempat yang tak ada, lihat DECISIONS.md).
 */
export function NotificationPanel({ items, onMarkRead, onMarkAllRead, align = "right" }: NotificationPanelProps) {
  const hasUnread = items.some((n) => !n.isRead);

  return (
    <div className={`absolute ${align === "left" ? "left-0" : "right-0"} top-full z-40 mt-2 w-[min(20rem,calc(100vw-2rem))] rounded-[var(--radius-lg)] border border-border bg-surface shadow-lg`}>
      <div className="flex items-center justify-between border-b border-border p-3">
        <span className="text-sm font-semibold text-ink">Notifikasi</span>
        {hasUnread && (
          <button
            type="button"
            onClick={onMarkAllRead}
            className="min-h-[var(--tap-min)] rounded-[var(--radius-sm)] px-2 text-xs font-medium text-primary outline-none transition-[color,background-color,border-color] hover:bg-primary-muted focus-visible:outline-2 focus-visible:outline-focus active:bg-primary-muted active:underline"
          >
            Tandai semua dibaca
          </button>
        )}
      </div>

      <div className="max-h-96 overflow-y-auto">
        {items.length === 0 && <p className="p-4 text-center text-sm text-ink-muted">Belum ada notifikasi.</p>}

        {items.length > 0 && (
          <ul>
            {items.map((n) => {
              const downloadUrl = n.type === "ExportReady" ? extractDownloadUrl(n.payloadJson) : null;
              const journalId = ["JournalApproved", "JournalRejected", "TeacherComment"].includes(n.type)
                ? extractJournalId(n.payloadJson)
                : null;
              const journalHref = journalId ? `/student/history?journalId=${encodeURIComponent(journalId)}` : null;
              return (
                <li key={n.id}>
                  <div className={
                    "flex items-start gap-2 border-b border-border p-3 text-left text-sm last:border-b-0 " +
                    (n.isRead ? "text-ink-muted" : "bg-primary-muted/40 font-medium text-ink")
                  }>
                    <Icon name={ICON_BY_TYPE[n.type] ?? "bell"} size={20} className="shrink-0" />
                    <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                      <span>{LABEL_BY_TYPE[n.type] ?? n.type}</span>
                      <span className="text-xs text-ink-muted">
                        {new Date(n.createdAt).toLocaleString("id-ID", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                      </span>
                      {journalHref && (
                        <a
                          href={journalHref}
                          onClick={() => !n.isRead && onMarkRead(n.id)}
                          className="mt-1 inline-flex min-h-[var(--tap-min)] items-center text-xs font-semibold text-primary underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
                        >
                          Buka jurnal terkait
                        </a>
                      )}
                    </span>
                    {!journalHref && (
                      <button
                        type="button"
                        aria-label={n.isRead ? undefined : "Tandai notifikasi sudah dibaca"}
                        onClick={() => !n.isRead && onMarkRead(n.id)}
                        className="min-h-[var(--tap-min)] min-w-[var(--tap-min)] rounded-[var(--radius-sm)] outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
                      >
                        <span className="sr-only">Tandai sudah dibaca</span>
                      </button>
                    )}
                  </div>
                  {downloadUrl && (
                    <a
                      href={downloadUrl}
                      onClick={(e) => e.stopPropagation()}
                      className="mb-1 ml-9 inline-flex min-h-[var(--tap-min)] items-center gap-1.5 rounded-[var(--radius-sm)] px-2 text-xs font-medium text-primary outline-none transition-[color,background-color,border-color] hover:bg-primary-muted focus-visible:outline-2 focus-visible:outline-focus active:bg-primary-muted active:underline"
                    >
                      <Icon name="download" size={16} /> Unduh file export
                    </a>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}
