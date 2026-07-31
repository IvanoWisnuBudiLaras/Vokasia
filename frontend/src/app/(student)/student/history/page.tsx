import { cn } from "@/lib/cn";
import { PageHeading } from "@/components/PageHeading";
import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { JournalDto, Paged } from "@/lib/apiTypes";
import { JournalEntryStatus } from "@/lib/apiTypes";
import { JournalHistoryList } from "./JournalHistoryList";

export const dynamic = "force-dynamic";

const TABS: { key: string; label: string; status: number | null }[] = [
  { key: "all", label: "Semua", status: null },
  { key: "pending", label: "Menunggu", status: JournalEntryStatus.Submitted },
  { key: "approved", label: "Disetujui", status: JournalEntryStatus.Approved },
  { key: "rejected", label: "Ditolak", status: JournalEntryStatus.Rejected },
];

/** VOK-H3-E2 §1 student/history/page.tsx — riwayat berfilter status via tab & berpaginasi. */
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
    const query = activeTab.status !== null ? `?status=${activeTab.status}&pageSize=200` : "?pageSize=200";
    const paged = await fetcher<Paged<JournalDto>>(`/journals${query}`);
    items = paged.items;
  } catch (err) {
    console.error("[student/history] gagal memuat riwayat jurnal:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        eyebrow="PROGRES BELAJAR"
        title="Riwayat jurnal"
        description="Lihat kegiatan yang sudah dikirim dan tanggapan dari mentor."
      />

      <div className="flex gap-1 overflow-x-auto rounded-[var(--radius-md)] bg-surface-muted p-1">
        {TABS.map((tab) => (
          <a
            key={tab.key}
            href={tab.key === "all" ? "/student/history" : `/student/history?status=${tab.key}`}
            className={cn(
              "flex min-h-[var(--tap-min)] flex-1 items-center justify-center whitespace-nowrap rounded-[var(--radius-sm)] px-3 text-sm font-medium outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2",
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
          icon={<Icon name="calendar-days" size={32} />}
          title="Belum ada riwayat jurnal"
          description="Jurnal yang sudah kamu kirim akan muncul di sini, lengkap dengan status persetujuan mentor."
        />
      )}

      {items.length > 0 && <JournalHistoryList items={items} />}
    </div>
  );
}
