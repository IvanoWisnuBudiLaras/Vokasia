import { cn } from "@/lib/cn";
import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { JournalDto, Paged } from "@/lib/apiTypes";
import { JournalEntryStatus } from "@/lib/apiTypes";
import { JournalHistoryList } from "./JournalHistoryList";

export const dynamic = "force-dynamic";

const TABS: { key: string; label: string; status: string | null }[] = [
  { key: "all", label: "Semua", status: null },
  { key: "pending", label: "Menunggu", status: "Submitted" },
  { key: "approved", label: "Disetujui", status: "Approved" },
  { key: "rejected", label: "Perlu revisi", status: "Rejected" },
];

function historyHref(status: string, from?: string, to?: string) {
  const query = new URLSearchParams();
  if (status !== "all") query.set("status", status);
  if (from) query.set("from", from);
  if (to) query.set("to", to);
  const encoded = query.toString();
  return encoded ? `/student/history?${encoded}` : "/student/history";
}

/** VOK-H3-E2 §1 student/history/page.tsx — riwayat berfilter status via tab & berpaginasi. */
export default async function StudentHistoryPage({
  searchParams,
}: {
  searchParams: Promise<{ status?: string; from?: string; to?: string; journalId?: string }>;
}) {
  const params = await searchParams;
  const activeTab = TABS.find((t) => t.key === params.status) ?? TABS[0];

  let items: JournalDto[] = [];
  let loadError = false;

  try {
    const queryParams = new URLSearchParams({ pageSize: "200" });
    if (activeTab.status !== null) queryParams.set("status", String(activeTab.status));
    if (params.from) queryParams.set("from", params.from);
    if (params.to) queryParams.set("to", params.to);
    const query = `?${queryParams.toString()}`;
    const paged = await fetcher<Paged<JournalDto>>(`/journals${query}`);
    items = paged.items;
  } catch (err) {
    console.error("[student/history] gagal memuat riwayat jurnal:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-1 border-b border-border pb-4">
        <h1 className="text-2xl font-bold tracking-tight text-ink">Riwayat jurnal</h1>
        <p className="text-sm leading-6 text-ink-muted">Lihat kegiatan yang sudah dikirim dan tanggapan dari mentor.</p>
      </div>

      <nav aria-label="Filter status jurnal" className="flex gap-1 overflow-x-auto border-b border-border pb-1">
        {TABS.map((tab) => (
          <a
            key={tab.key}
            href={historyHref(tab.key, params.from, params.to)}
            aria-current={tab.key === activeTab.key ? "page" : undefined}
            className={cn(
              "flex min-h-[var(--tap-min)] items-center justify-center whitespace-nowrap border-b-2 px-3 text-sm font-medium outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2",
              tab.key === activeTab.key ? "border-primary text-ink" : "border-transparent text-ink-muted hover:text-ink"
            )}
          >
            {tab.label}
          </a>
        ))}
      </nav>

      <details className="border-b border-border pb-3">
        <summary className="min-h-[var(--tap-min)] cursor-pointer text-sm font-medium text-primary outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2">
          Filter lainnya
        </summary>
        <form method="get" className="mt-3 grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end">
          {params.status && <input type="hidden" name="status" value={params.status} />}
          <label className="flex flex-col gap-1 text-sm font-medium text-ink">
            Dari tanggal
            <input name="from" type="date" defaultValue={params.from} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-focus" />
          </label>
          <label className="flex flex-col gap-1 text-sm font-medium text-ink">
            Sampai tanggal
            <input name="to" type="date" defaultValue={params.to} className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-3 text-base outline-none focus-visible:outline-2 focus-visible:outline-focus" />
          </label>
          <button type="submit" className="min-h-[var(--tap-min)] rounded-[var(--radius-md)] bg-primary px-4 text-sm font-semibold text-primary-ink outline-none hover:bg-primary-hover focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2">
            Terapkan filter
          </button>
        </form>
      </details>

      {loadError && <ErrorState message="Riwayat belum bisa dimuat. Coba muat ulang halaman." />}

      {!loadError && items.length === 0 && (
        <EmptyState
          icon={<Icon name="calendar-days" size={32} />}
          title="Belum ada riwayat jurnal"
          description="Jurnal yang sudah kamu kirim akan muncul di sini, lengkap dengan status persetujuan mentor."
        />
      )}

      {items.length > 0 && <JournalHistoryList items={items} highlightId={params.journalId} />}
    </div>
  );
}
