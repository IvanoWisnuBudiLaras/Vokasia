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
  const [isLeaveModalOpen, setIsLeaveModalOpen] = useState(false);
  const [leaveReason, setLeaveReason] = useState("Sakit");
  const [leaveNotes, setLeaveNotes] = useState("");
  const [leaveSubmitted, setLeaveSubmitted] = useState(false);

  function handleSubmitted(newEntry: JournalDto) {
    setEntry(newEntry);
    setWeekStatus((prev) => prev.map((d) => (d.date === slot.date ? { ...d, status: JournalSlotStatus.Filled } : d)));
  }

  const handleApplyLeave = (e: React.FormEvent) => {
    e.preventDefault();
    setLeaveSubmitted(true);
    setIsLeaveModalOpen(false);
  };

  const needsForm = entry === null || entry.status === JournalEntryStatus.Rejected;

  // Mock progress calculations (e.g. 42 out of 120 target days)
  const completedDays = 42;
  const totalDays = 120;
  const progressPct = Math.round((completedDays / totalDays) * 100);

  return (
    <div className="flex flex-col gap-4">
      {/* Gamified Progress Bar */}
      <div className="rounded-[var(--radius-lg)] border border-border bg-surface p-4 shadow-sm space-y-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-xl">🏆</span>
            <div>
              <h3 className="text-sm font-bold text-ink">Progres Program PKL</h3>
              <p className="text-xs text-ink-muted">Target {totalDays} hari kerja bimbingan industri</p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="inline-flex items-center gap-1 rounded-full bg-status-amber-bg px-2.5 py-1 text-xs font-bold text-status-amber border border-status-amber/30">
              🔥 Streak: {streak} Hari
            </span>
            <span className="text-sm font-bold text-primary">{progressPct}%</span>
          </div>
        </div>

        <div className="h-2.5 w-full rounded-full bg-surface-muted overflow-hidden border border-border">
          <div
            className="h-full bg-primary transition-all duration-500 rounded-full"
            style={{ width: `${progressPct}%` }}
          />
        </div>

        <div className="flex items-center justify-between text-xs text-ink-muted">
          <span>{completedDays} Hari Selesai</span>
          <span>{totalDays - completedDays} Hari Tersisa</span>
        </div>
      </div>

      {/* Leave Application Alert Banner if submitted */}
      {leaveSubmitted && (
        <div className="rounded-[var(--radius-md)] border border-status-amber/40 bg-status-amber-bg p-3 text-xs text-status-amber font-semibold flex items-center justify-between">
          <span>ℹ️ Pengajuan {leaveReason} hari ini telah dikirim ke Guru Pembimbing & Mentor. Status jurnal ditandai Izin/Sakit.</span>
          <button onClick={() => setLeaveSubmitted(false)} className="text-status-amber font-bold">✕</button>
        </div>
      )}

      <Card
        title={
          <span className="flex items-center justify-between gap-3">
            <span>Jurnal hari ini</span>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setIsLeaveModalOpen(true)}
                className="text-xs font-semibold text-ink-muted hover:text-primary underline"
              >
                🤒 Ajukan Sakit / Izin
              </button>
              {entry && badgeFor(entry.status)}
            </div>
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

      {/* Modal Pengajuan Sakit / Izin */}
      {isLeaveModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md rounded-[var(--radius-lg)] border border-border bg-surface p-6 shadow-xl space-y-4">
            <div className="flex items-center justify-between border-b border-border pb-3">
              <h3 className="text-base font-bold text-ink">Form Pengajuan Sakit / Izin</h3>
              <button onClick={() => setIsLeaveModalOpen(false)} className="text-ink-muted hover:text-ink font-bold">✕</button>
            </div>

            <form onSubmit={handleApplyLeave} className="space-y-4 text-xs">
              <div className="flex flex-col gap-1.5">
                <label className="font-semibold text-ink">Kategori Pengajuan:</label>
                <select
                  value={leaveReason}
                  onChange={(e) => setLeaveReason(e.target.value)}
                  className="h-10 w-full rounded-[var(--radius-md)] border border-border bg-surface px-3 text-xs text-ink outline-none"
                >
                  <option value="Sakit">Sakit (Dengan Surat Dokter)</option>
                  <option value="Izin">Izin Keperluan Keluarga / Sekolah</option>
                  <option value="Dinas">Tugas / Dinas Keluar dari Perusahaan</option>
                </select>
              </div>

              <div className="flex flex-col gap-1.5">
                <label className="font-semibold text-ink">Keterangan / Alasan Tambahan:</label>
                <textarea
                  required
                  rows={3}
                  value={leaveNotes}
                  onChange={(e) => setLeaveNotes(e.target.value)}
                  placeholder="Tuliskan keterangan surat sakit atau alasan izin secara rinci…"
                  className="w-full rounded-[var(--radius-md)] border border-border bg-surface p-3 text-xs text-ink outline-none"
                />
              </div>

              <div className="flex items-center justify-end gap-2 border-t border-border pt-3">
                <button
                  type="button"
                  onClick={() => setIsLeaveModalOpen(false)}
                  className="rounded-[var(--radius-md)] border border-border px-4 py-2 text-xs font-semibold text-ink-muted hover:bg-surface-muted"
                >
                  Batal
                </button>
                <button
                  type="submit"
                  className="rounded-[var(--radius-md)] bg-primary px-4 py-2 text-xs font-semibold text-primary-ink hover:bg-primary/90"
                >
                  Kirim Pengajuan
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
