"use client";

import { useState } from "react";
import { Card, StatusBadge } from "@/components/ui";
import { MaterialIcon } from "@/components/ui/MaterialIcon";
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

function statusBadge(status: number) {
  if (status === JournalEntryStatus.Approved) return <StatusBadge status="green" label="Disetujui" />;
  if (status === JournalEntryStatus.Rejected) return <StatusBadge status="red" label="Perlu diperbaiki" />;
  return <StatusBadge status="amber" label="Menunggu persetujuan" />;
}

export function TodayJournalCard({ slot, initialEntry, competencies, initialWeekStatus, streak, draftScope }: TodayJournalCardProps) {
  const [entry, setEntry] = useState(initialEntry);
  const [weekStatus, setWeekStatus] = useState(initialWeekStatus);

  function handleSubmitted(nextEntry: JournalDto) {
    setEntry(nextEntry);
    setWeekStatus((previous) => previous.map((day) => day.date === slot.date ? { ...day, status: JournalSlotStatus.Filled } : day));
  }

  const needsForm = entry === null || entry.status === JournalEntryStatus.Rejected;

  return (
    <div className="flex flex-col gap-4">
      <Card title="Jurnal hari ini">
        <div className="mb-4 flex items-center justify-between gap-3 border-b border-border pb-3">
          <div className="flex items-center gap-2 text-sm text-ink-muted">
            <MaterialIcon name="journal" decorative />
            <span>Streak jurnal: <strong className="text-ink">{streak} hari</strong></span>
          </div>
          {entry && statusBadge(entry.status)}
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
            <p className="whitespace-pre-wrap text-sm leading-6 text-ink">{entry.text}</p>
            {entry.mentorNote && <p className="text-sm text-ink-muted">Catatan mentor: {entry.mentorNote}</p>}
            {entry.photos.length > 0 && <p className="inline-flex items-center gap-1 text-xs text-ink-muted"><MaterialIcon name="journal" decorative />{entry.photos.length} foto terlampir</p>}
          </div>
        )}
      </Card>

      <WeekStrip days={weekStatus} streak={streak} />
    </div>
  );
}
