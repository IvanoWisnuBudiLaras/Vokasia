"use client";

import { useState } from "react";
import { Card, StatusBadge } from "@/components/ui";
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
export function TodayJournalCard({ slot, initialEntry, competencies, initialWeekStatus, streak }: TodayJournalCardProps) {
  const [entry, setEntry] = useState(initialEntry);
  const [weekStatus, setWeekStatus] = useState(initialWeekStatus);

  function handleSubmitted(newEntry: JournalDto) {
    setEntry(newEntry);
    setWeekStatus((prev) => prev.map((d) => (d.date === slot.date ? { ...d, status: JournalSlotStatus.Filled } : d)));
  }

  const needsForm = entry === null || entry.status === JournalEntryStatus.Rejected;

  return (
    <div className="flex flex-col gap-4">
      <Card title="📓 Jurnal Hari Ini">
        {needsForm ? (
          <JournalForm
            slot={slot}
            competencies={competencies}
            rejectedReason={entry?.status === JournalEntryStatus.Rejected ? entry.mentorNote : null}
            onSubmitted={handleSubmitted}
          />
        ) : (
          <div className="flex flex-col gap-3">
            <div>{badgeFor(entry.status)}</div>
            <p className="whitespace-pre-wrap text-sm text-ink">{entry.text}</p>
            {entry.status === JournalEntryStatus.Approved && entry.mentorNote && (
              <p className="text-sm text-ink-muted">Catatan mentor: {entry.mentorNote}</p>
            )}
            {entry.photos.length > 0 && <p className="text-xs text-ink-muted">📎 {entry.photos.length} foto terlampir</p>}
          </div>
        )}
      </Card>

      <WeekStrip days={weekStatus} streak={streak} />
    </div>
  );
}
