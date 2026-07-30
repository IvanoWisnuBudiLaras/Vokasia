"use client";

import { useState } from "react";
import { Card, Icon, StatusBadge } from "@/components/ui";
import type { CompetencyDto, JournalDto, JournalSlotDto, WeekDayStatusDto } from "@/lib/apiTypes";
import { JournalEntryStatus, JournalSlotStatus } from "@/lib/apiTypes";
import { JournalForm } from "./JournalForm";
import { WeekStrip } from "./WeekStrip";

interface TodayJournalCardProps {
  slot: JournalSlotDto;
  initialEntry: JournalDto | null;
  competencies: CompetencyDto[];
  initialWeekStatus: WeekDayStatusDto[];
  streak: number;
  draftScope: string | null;
}

function badgeFor(status: number) {
  if (status === JournalEntryStatus.Approved) return <StatusBadge status="green" label="Disetujui" />;
  if (status === JournalEntryStatus.Rejected) return <StatusBadge status="red" label="Ditolak" />;
  return <StatusBadge status="amber" label="Menunggu persetujuan mentor" />;
}

/**
 * VOK-H3-E2 §1 — pemilik state klien utk kartu jurnal hari ini + strip minggu, supaya keduanya
 * ikut ter-update BERSAMAAN begitu submit sukses ("optimistic update status hari" — AC ticket)
 * TANPA round-trip fetch server kedua: hasil SubmitJournal (JournalDto) langsung jadi state baru.
 */
export function TodayJournalCard({ slot, initialEntry, competencies, initialWeekStatus, streak, draftScope }: TodayJournalCardProps) {
  const [entry, setEntry] = useState(initialEntry);
  const [weekStatus, setWeekStatus] = useState(initialWeekStatus);

  function handleSubmitted(newEntry: JournalDto) {
    setEntry(newEntry);
    setWeekStatus((prev) => prev.map((d) => (d.date === slot.date ? { ...d, status: JournalSlotStatus.Filled } : d)));
  }

  const needsForm = entry === null || entry.status === JournalEntryStatus.Rejected;

  return (
    <div className="flex flex-col gap-4">
      <Card
        title={
          <span className="flex items-center justify-between gap-3">
            <span>Jurnal hari ini</span>
            {entry && badgeFor(entry.status)}
          </span>
        }
      >
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
            <p className="whitespace-pre-wrap text-sm leading-6 text-ink">{entry.text}</p>
            {entry.status === JournalEntryStatus.Approved && entry.mentorNote && (
              <p className="text-sm text-ink-muted">Catatan mentor: {entry.mentorNote}</p>
            )}
            {entry.photos.length > 0 && (
              <p className="inline-flex items-center gap-1 text-xs text-ink-muted">
                <Icon name="image" size={16} /> {entry.photos.length} foto terlampir
              </p>
            )}
          </div>
        )}
      </Card>

      <WeekStrip days={weekStatus} streak={streak} />
    </div>
  );
}
