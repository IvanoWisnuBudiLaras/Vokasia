"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui";
import { RichTextEditor } from "@/components/ui/RichTextEditor";
import { apiClient, ApiError } from "@/lib/apiClient";
import type { CompetencyDto, JournalDto, JournalSlotDto } from "@/lib/apiTypes";
import { richTextPlainText } from "@/lib/richText";
import { CompetencyPicker } from "./CompetencyPicker";
import { PhotoUploader, type PendingPhoto } from "./PhotoUploader";
import { journalDraftKey, parseJournalDraft, serializeJournalDraft } from "./journalDraft";

interface JournalFormProps {
  slot: JournalSlotDto;
  competencies: CompetencyDto[];
  draftScope: string | null;
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
export function JournalForm({ slot, competencies, draftScope, rejectedReason, onSubmitted }: JournalFormProps) {
  const [text, setText] = useState("");
  const [competencyIds, setCompetencyIds] = useState<string[]>([]);
  const [photos, setPhotos] = useState<PendingPhoto[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [draftSaved, setDraftSaved] = useState(false);
  const [offline, setOffline] = useState(false);

  const draftKey = draftScope ? journalDraftKey(draftScope, slot.id) : null;

  useEffect(() => {
    if (!draftKey) return;

    let cancelled = false;
    queueMicrotask(() => {
      if (cancelled) return;

      try {
        const validCompetencyIds = new Set(competencies.map((competency) => competency.id));
        const draft = parseJournalDraft(sessionStorage.getItem(draftKey), validCompetencyIds);
        if (draft) {
          setText(draft.text);
          setCompetencyIds(draft.competencyIds);
          setDraftSaved(true);
        }
      } catch {
        // Storage bisa dibatasi browser; form tetap harus dapat digunakan.
      }
    });

    return () => {
      cancelled = true;
    };
  }, [competencies, draftKey]);

  useEffect(() => {
    const update = () => setOffline(!navigator.onLine);
    update();
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => { window.removeEventListener("online", update); window.removeEventListener("offline", update); };
  }, []);

  function persistDraft(nextText: string, nextCompetencyIds: string[]) {
    if (!draftKey) return;
    try {
      if (nextText.trim().length === 0 && nextCompetencyIds.length === 0) {
        sessionStorage.removeItem(draftKey);
        setDraftSaved(false);
        return;
      }

      sessionStorage.setItem(draftKey, serializeJournalDraft(nextText, nextCompetencyIds));
      setDraftSaved(true);
    } catch {
      // Storage bisa dibatasi browser; form tetap harus dapat digunakan.
    }
  }

  const stillUploading = photos.some((p) => p.status === "uploading");
  const hasFailedPhoto = photos.some((p) => p.status === "error");
  const plainText = richTextPlainText(text);
  const hasPhotos = photos.length > 0 && photos.some((p) => p.status === "uploaded");
  const canSubmit =
    plainText.trim().length > 0 &&
    plainText.length <= MAX_TEXT &&
    competencyIds.length > 0 &&
    hasPhotos &&
    !submitting &&
    !stillUploading &&
    !offline;

  function handleTextChange(value: string) {
    setText(value);
    setDraftSaved(false);
    persistDraft(value, competencyIds);
  }

  function handleCompetenciesChange(ids: string[]) {
    setCompetencyIds(ids);
    setDraftSaved(false);
    persistDraft(text, ids);
  }

  async function handleSubmit() {
    setError(null);

    if (plainText.trim().length === 0) {
      setError("Tulis dulu apa yang kamu kerjakan hari ini.");
      return;
    }
    if (plainText.length > MAX_TEXT) {
      setError(`Jurnal maksimal ${MAX_TEXT} karakter.`);
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

      if (draftKey) {
        try {
          sessionStorage.removeItem(draftKey);
        } catch {
          // Pengiriman sudah berhasil; kegagalan membersihkan storage tidak boleh menggagalkannya.
        }
      }

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
        <div role="alert" className="rounded-[var(--radius-md)] border border-status-red/30 bg-status-red-bg p-3 text-sm text-status-red">
          <strong className="font-semibold">Catatan mentor:</strong> {rejectedReason}
        </div>
      )}

      {offline && <p role="status" className="border border-status-amber/40 bg-status-amber-bg p-3 text-sm text-status-amber">Anda sedang offline. Draf tetap tersimpan di sesi browser; sambungkan internet untuk mengirim jurnal.</p>}

      {draftSaved && draftScope && <span role="status" aria-live="polite" className="inline-flex items-center gap-1 text-xs text-ink-muted"><span aria-hidden="true">✓</span> Tersimpan</span>}

      <RichTextEditor
        label="Apa yang kamu kerjakan hari ini?"
        value={text}
        onChange={handleTextChange}
        disabled={submitting}
        maxLength={MAX_TEXT}
      />

      <CompetencyPicker options={competencies} selected={competencyIds} max={MAX_COMPETENCIES} onChange={handleCompetenciesChange} />

      <PhotoUploader max={MAX_PHOTOS} photos={photos} setPhotos={setPhotos} disabled={submitting} />

      {error && <p role="alert" className="rounded-[var(--radius-md)] border border-status-red/30 bg-status-red-bg p-3 text-sm font-medium text-status-red">{error}</p>}

      <Button size="lg" className="w-full" onClick={handleSubmit} loading={submitting} disabled={!canSubmit}>
        {rejectedReason ? "Kirim ulang jurnal" : "Kirim jurnal"}
      </Button>
    </div>
  );
}
