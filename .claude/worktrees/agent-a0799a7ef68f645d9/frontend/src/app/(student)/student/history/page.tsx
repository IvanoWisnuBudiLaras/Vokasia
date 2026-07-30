import { cn } from "@/lib/cn";
import { EmptyState, ErrorState, StatusBadge } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { JournalDto, Paged } from "@/lib/apiTypes";
import { JournalEntryStatus } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

const TABS: { key: string; label: string; status: number | null }[] = [
  { key: "all", label: "Semua", status: null },
  { key: "pending", label: "Menunggu", status: JournalEntryStatus.Submitted },
  { key: "approved", label: "Disetujui", status: JournalEntryStatus.Approved },
  { key: "rejected", label: "Ditolak", status: JournalEntryStatus.Rejected },
];

function badgeFor(status: number) {
  if (status === JournalEntryStatus.Approved) return <StatusBadge status="green" label="Disetujui" />;
  if (status === JournalEntryStatus.Rejected) return <StatusBadge status="red" label="Ditolak" />;
  return <StatusBadge status="amber" label="Menunggu" />;
}

function JournalHistoryItem({ journal }: { journal: JournalDto }) {
  const date = new Date(journal.submittedAt).toLocaleDateString("id-ID", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

  return (
    <li className="flex flex-col gap-1.5 rounded-[var(--radius-md)] border border-border bg-surface p-3">
      <div className="flex items-center justify-between">
        <span className="text-xs text-ink-muted">{date}</span>
        {badgeFor(journal.status)}
      </div>
      <p className="line-clamp-3 text-sm text-ink">{journal.text}</p>
      {journal.status === JournalEntryStatus.Rejected && journal.mentorNote && (
        <p className="text-xs text-status-red">Alasan: {journal.mentorNote}</p>
      )}
      {journal.photos.length > 0 && <p className="text-xs text-ink-muted">📎 {journal.photos.length} foto</p>}
    </li>
  );
}

/** VOK-H3-E2 §1 student/history/page.tsx + JournalHistoryItem — riwayat berfilter status via tab. */
export default async function StudentHistoryPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string }>;
}) {
  const params = await searchParams;
  const activeTab = TABS.find((t) => t.key === params.status) ?? TABS[0];

  let items: JournalDto[] = [];
  let loadError = false;

  try {
    const query = activeTab.status !== null ? `?status=${activeTab.status}&pageSize=50` : "?pageSize=50";
    const paged = await fetcher<Paged<JournalDto>>(`/journals${query}`);
    items = paged.items;
  } catch (err) {
    console.error("[student/history] gagal memuat riwayat jurnal:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-lg font-semibold text-ink">Riwayat Jurnal</h1>

      <div className="flex gap-1 overflow-x-auto rounded-[var(--radius-md)] bg-surface-muted p-1">
        {TABS.map((tab) => (
          <a
            key={tab.key}
            href={tab.key === "all" ? "/student/history" : `/student/history?status=${tab.key}`}
            className={cn(
              "flex h-9 flex-1 items-center justify-center whitespace-nowrap rounded-[var(--radius-sm)] px-3 text-sm font-medium",
              tab.key === activeTab.key ? "bg-surface text-ink shadow-sm" : "text-ink-muted"
            )}
          >
            {tab.label}
          </a>
        ))}
      </div>

      {loadError && <ErrorState message="Riwayat belum bisa dimuat. Coba muat ulang halaman." />}

      {!loadError && items.length === 0 && (
        <EmptyState
          icon="🗓️"
          title="Belum ada riwayat jurnal"
          description="Jurnal yang sudah kamu kirim akan muncul di sini, lengkap dengan status persetujuan mentor."
        />
      )}

      {items.length > 0 && (
        <ul className="flex flex-col gap-2">
          {items.map((j) => (
            <JournalHistoryItem key={j.id} journal={j} />
          ))}
        </ul>
      )}
    </div>
  );
}
