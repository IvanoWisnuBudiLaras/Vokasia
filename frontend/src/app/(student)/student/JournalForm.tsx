"use client";

import { useState } from "react";
import { Button, Textarea } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { CompetencyDto, JournalDto, JournalSlotDto } from "@/lib/apiTypes";
import { CompetencyPicker } from "./CompetencyPicker";
import { PhotoUploader, type PendingPhoto } from "./PhotoUploader";

interface JournalFormProps {
  slot: JournalSlotDto;
  competencies: CompetencyDto[];
  rejectedReason?: string | null;
  onSubmitted: (entry: JournalDto) => void;
}

const MAX_TEXT = 500;
const MAX_COMPETENCIES = 5;
const MAX_PHOTOS = 3;

/**
 * VOK-H3-E2 §1 JournalForm({slot, competencies, onSubmitted}). Textarea <=500 + counter live,
 * kompetensi chips maks 5, tombol KIRIM JURNAL besar (size lg, >=44px tap target), disable saat
 * submit, sukses -> optimistic update (lihat TodayJournalCard.handleSubmitted, bukan router
 * refresh — hasil SubmitJournal langsung dipakai sbg state baru tanpa round-trip server kedua).
 */
export function JournalForm({ slot, competencies, rejectedReason, onSubmitted }: JournalFormProps) {
  const [text, setText] = useState("");
  const [competencyIds, setCompetencyIds] = useState<string[]>([]);
  const [photos, setPhotos] = useState<PendingPhoto[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const stillUploading = photos.some((p) => p.status === "uploading");
  const hasFailedPhoto = photos.some((p) => p.status === "error");
  const canSubmit = text.trim().length > 0 && !submitting && !stillUploading;

  async function handleSubmit() {
    setError(null);

    if (text.trim().length === 0) {
      setError("Tulis dulu apa yang kamu kerjakan hari ini.");
      return;
    }
    if (hasFailedPhoto) {
      setError("Ada foto yang gagal diunggah — ulangi atau hapus dulu sebelum kirim.");
      return;
    }

    setSubmitting(true);
    try {
      // Langkah 1: kirim teks+kompetensi dulu (PhotoIds kosong - lihat catatan panjang di
      // PhotoUploader.tsx utk kenapa foto TIDAK bisa ditempel sblm entry ini ada).
      const entry = await apiClient.post<JournalDto>(`/journals/${slot.id}/submit`, {
        slotId: slot.id,
        text: text.trim(),
        competencyIds,
        photoIds: [],
      });

      // Langkah 2: tempel tiap foto yang sudah sukses di-presign+PUT ke MinIO, pakai entry.id yang
      // baru saja didapat. Jurnal TEKS sudah tersimpan di titik ini — kegagalan tempel 1 foto tak
      // membatalkan submission (AC: "tanpa membatalkan form"); siswa tetap lihat konfirmasi terkirim.
      for (const photo of photos) {
        if (!photo.objectKey) continue;
        try {
          await apiClient.post(`/journals/${entry.id}/photos`, { objectKey: photo.objectKey });
        } catch {
          // Diam-diam dilewati secara sengaja: jurnal induk sudah berhasil, ini best-effort.
        }
      }

      onSubmitted(entry);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal mengirim jurnal. Coba lagi.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {rejectedReason && (
        <div className="rounded-[var(--radius-md)] border border-status-red/30 bg-status-red-bg p-3 text-sm text-status-red">
          <strong className="font-semibold">Jurnal sebelumnya ditolak:</strong> {rejectedReason}
        </div>
      )}

      <Textarea
        label="Apa yang kamu kerjakan hari ini?"
        maxLength={MAX_TEXT}
        showCounter
        value={text}
        onChange={(e) => setText(e.target.value)}
        disabled={submitting}
        placeholder="Ceritakan singkat kegiatan PKL-mu hari ini..."
      />

      <CompetencyPicker options={competencies} selected={competencyIds} max={MAX_COMPETENCIES} onChange={setCompetencyIds} />

      <PhotoUploader max={MAX_PHOTOS} photos={photos} setPhotos={setPhotos} disabled={submitting} />

      {error && <p className="text-sm text-status-red">{error}</p>}

      <Button size="lg" className="w-full" onClick={handleSubmit} loading={submitting} disabled={!canSubmit}>
        Kirim Jurnal
      </Button>
    </div>
  );
}
