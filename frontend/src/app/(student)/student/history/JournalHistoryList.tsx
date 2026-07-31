"use client";

import { useState } from "react";
import { Icon, Pagination, StatusBadge } from "@/components/ui";
import type { JournalDto } from "@/lib/apiTypes";
import { JournalEntryStatus } from "@/lib/apiTypes";

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
      {journal.photos.length > 0 && (
        <p className="inline-flex items-center gap-1 text-xs text-ink-muted">
          <Icon name="image" size={16} /> {journal.photos.length} foto
        </p>
      )}
    </li>
  );
}

export function JournalHistoryList({ items }: { items: JournalDto[] }) {
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);

  const paginated = items.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  return (
    <div className="flex flex-col gap-3">
      <ul className="flex flex-col gap-2">
        {paginated.map((j) => (
          <JournalHistoryItem key={j.id} journal={j} />
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
