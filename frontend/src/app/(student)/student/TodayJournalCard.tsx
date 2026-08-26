"use client";

import { useState } from "react";
import { StatusBadge } from "@/components/ui";
import { MaterialIcon } from "@/components/ui/MaterialIcon";
import { RichTextContent } from "@/components/ui/RichTextContent";
import type { CompetencyDto, JournalDto, JournalSlotDto } from "@/lib/apiTypes";
import { JournalEntryStatus } from "@/lib/apiTypes";
import { JournalForm } from "./JournalForm";

interface TodayJournalCardProps {
  slot: JournalSlotDto;
  initialEntry: JournalDto | null;
  competencies: CompetencyDto[];
  draftScope: string | null;
}

function submittedAgo(iso: string): string {
  const minutes = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 60_000));
  if (minutes < 1) return "baru saja";
  if (minutes < 60) return `${minutes} menit lalu`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} jam lalu`;
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "short" });
}

function statusBadge(entry: JournalDto) {
  if (entry.status === JournalEntryStatus.Approved) {
    return <StatusBadge status="green" label="Disetujui mentor ✓" />;
  }
  if (entry.status === JournalEntryStatus.Rejected) {
    return <StatusBadge status="red" label="Perlu revisi" />;
  }
  return <StatusBadge status="amber" label={`Menunggu review mentor · dikirim ${submittedAgo(entry.submittedAt)}`} />;
}

function todayStatus(entry: JournalDto | null): string {
  if (!entry) return "Belum diisi";
  if (entry.status === JournalEntryStatus.Approved) return "Disetujui mentor";
  if (entry.status === JournalEntryStatus.Rejected) return "Perlu revisi";
  return "Menunggu review mentor";
}

export function TodayJournalCard({ slot, initialEntry, competencies, draftScope }: TodayJournalCardProps) {
  const [entry, setEntry] = useState(initialEntry);

  function handleSubmitted(nextEntry: JournalDto) {
    setEntry(nextEntry);
  }

  const needsForm = entry === null || entry.status === JournalEntryStatus.Rejected;

  return (
    <div className="flex flex-col gap-4">
      <section aria-labelledby="student-summary-heading" className="border-y border-border py-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 id="student-summary-heading" className="text-sm font-semibold text-ink">Tugas utama hari ini</h2>
            <p className="mt-1 text-sm text-ink-muted">Fokus pada jurnal yang perlu kamu selesaikan sekarang.</p>
          </div>
          <div className="flex gap-5 text-sm">
            <div>
              <span className="block text-xs text-ink-muted">Status jurnal</span>
              <span className="font-semibold text-ink">{todayStatus(entry)}</span>
            </div>
          </div>
        </div>
      </section>

      <section id="jurnal-hari-ini" aria-labelledby="today-journal-heading" className="border-y border-border bg-surface px-0 py-5 sm:px-1">
        <div className="mb-4 flex items-start justify-between gap-3 border-b border-border pb-3">
          <div className="flex items-center gap-2 text-sm text-ink-muted">
            <MaterialIcon name="journal" decorative />
            <h2 id="today-journal-heading" className="font-semibold text-ink">Jurnal hari ini</h2>
          </div>
          {entry && <div className="text-right">{statusBadge(entry)}</div>}
        </div>

        {needsForm ? (
          <JournalForm
            slot={slot}
            competencies={competencies}
            draftScope={draftScope}
            rejectedReason={entry?.status === JournalEntryStatus.Rejected ? entry.mentorNote : null}
            onSubmitted={handleSubmitted}
          />
        ) : (
          <div className="flex flex-col gap-3">
            <RichTextContent value={entry.text} className="flex flex-col gap-2 text-sm leading-6 text-ink" />
            {entry.mentorNote && <p className="text-sm text-ink-muted">Catatan mentor: {entry.mentorNote}</p>}
            {entry.photos.length > 0 && <p className="inline-flex items-center gap-1 text-xs text-ink-muted"><MaterialIcon name="journal" decorative />{entry.photos.length} foto terlampir</p>}
          </div>
        )}
      </section>
    </div>
  );
}
