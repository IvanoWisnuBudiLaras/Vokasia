"use client";

import { useState } from "react";
import { Icon, Pagination, StatusBadge } from "@/components/ui";
import { RichTextContent } from "@/components/ui/RichTextContent";
import type { JournalDto } from "@/lib/apiTypes";
import { JournalEntryStatus } from "@/lib/apiTypes";
import { richTextPlainText } from "@/lib/richText";

function badgeFor(status: number) {
  if (status === JournalEntryStatus.Approved) return <StatusBadge status="green" label="Disetujui" />;
  if (status === JournalEntryStatus.Rejected) return <StatusBadge status="red" label="Perlu revisi" />;
  return <StatusBadge status="amber" label="Menunggu" />;
}

function groupLabel(iso: string): string {
  const now = new Date();
  const date = new Date(iso);
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const itemDay = new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
  const daysAgo = Math.round((today - itemDay) / 86_400_000);
  if (daysAgo === 0) return "Hari ini";
  if (daysAgo === 1) return "Kemarin";
  if (daysAgo >= 2 && daysAgo < 7) return "Minggu ini";
  return "Sebelumnya";
}

function JournalHistoryItem({ journal, highlighted }: { journal: JournalDto; highlighted: boolean }) {
  const date = new Date(journal.submittedAt).toLocaleDateString("id-ID", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });

  return (
    <li id={`journal-${journal.id}`} className={highlighted ? "border-l-4 border-primary bg-primary-muted/30" : undefined}>
      <details open={highlighted} className="group border-b border-border">
        <summary className="flex min-h-[var(--tap-min)] cursor-pointer list-none items-center gap-3 py-3 outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 [&::-webkit-details-marker]:hidden">
          <span className="w-20 shrink-0 text-xs text-ink-muted">{date}</span>
          <span className="min-w-0 flex-1 line-clamp-2 text-sm text-ink">{richTextPlainText(journal.text).replace(/&nbsp;/g, " ")}</span>
          <span className="shrink-0">{badgeFor(journal.status)}</span>
        </summary>
        <div className="flex flex-col gap-2 pb-4 pl-20 pr-1 text-sm text-ink">
          <RichTextContent value={journal.text} className="flex flex-col gap-2 leading-6" />
          {journal.status === JournalEntryStatus.Rejected && journal.mentorNote && (
            <p className="text-sm text-status-red"><strong>Catatan mentor:</strong> {journal.mentorNote}</p>
          )}
          {journal.photos.length > 0 && (
            <p className="inline-flex items-center gap-1 text-xs text-ink-muted">
              <Icon name="image" size={16} /> {journal.photos.length} foto terlampir
            </p>
          )}
        </div>
      </details>
    </li>
  );
}

export function JournalHistoryList({ items, highlightId }: { items: JournalDto[]; highlightId?: string }) {
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);

  const paginated = items.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const grouped = paginated.reduce<Record<string, JournalDto[]>>((groups, journal) => {
    const label = groupLabel(journal.submittedAt);
    (groups[label] ??= []).push(journal);
    return groups;
  }, {});

  return (
    <div className="flex flex-col gap-3">
      <ul>
        {Object.entries(grouped).map(([label, journals]) => (
          <li key={label}>
            <h2 className="pb-1 pt-3 text-xs font-semibold uppercase tracking-wide text-ink-muted first:pt-0">{label}</h2>
            <ul>
              {journals.map((journal) => (
                <JournalHistoryItem key={journal.id} journal={journal} highlighted={journal.id === highlightId} />
              ))}
            </ul>
          </li>
        ))}
      </ul>

      <Pagination
        currentPage={currentPage}
        totalItems={items.length}
        pageSize={pageSize}
        onPageChange={(p) => setCurrentPage(p)}
        onPageSizeChange={(s) => {
          setPageSize(s);
          setCurrentPage(1);
        }}
      />
    </div>
  );
}
