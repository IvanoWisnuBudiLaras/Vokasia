"use client";

import { useState } from "react";
import { Button, Textarea } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
import { useFormDraft } from "@/lib/useFormDraft";
import { PhotoUploader, type PendingPhoto } from "@/app/(student)/student/PhotoUploader";
import { SignaturePad } from "./SignaturePad";

export interface VisitFormProps {
  placementId: string;
  onSubmitted: () => void;
}

const MAX_NOTES = 500;

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

/**
 * VOK-H5-E2 §1 VisitForm({placementId, onSubmitted}) — form W4 lengkap: tanggal, catatan, foto, ttd.
 * Draft tersimpan otomatis di localStorage dan dibersihkan setelah submit sukses.
 */
export function VisitForm({ placementId, onSubmitted }: VisitFormProps) {
  const { values, updateField, clearDraft } = useFormDraft(`visit_${placementId}`, {
    date: todayIso(),
    notes: "",
  });
  const [photos, setPhotos] = useState<PendingPhoto[]>([]);
  const [signatureDataUrl, setSignatureDataUrl] = useState<string | null>(null);
  const [signatureNonce, setSignatureNonce] = useState(0);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [justSaved, setJustSaved] = useState(false);

  const stillUploading = photos.some((p) => p.status === "uploading");
  const hasFailedPhoto = photos.some((p) => p.status === "error");
  const canSubmit = values.notes.trim().length > 0 && !submitting && !stillUploading;

  async function handleSubmit() {
    setError(null);
    setJustSaved(false);

    if (values.notes.trim().length === 0) {
      setError("Tulis dulu catatan kunjungan.");
      return;
    }
    if (hasFailedPhoto) {
      setError("Ada foto yang gagal diunggah — ulangi atau hapus dulu sebelum simpan.");
      return;
    }

    setSubmitting(true);
    try {
      await apiClient.post(`/placements/${placementId}/visits`, {
        date: values.date,
        notes: values.notes.trim(),
        photoKey: photos[0]?.objectKey ?? null,
        signatureDataUrl,
      });

      // Bersihkan draft localStorage
      clearDraft();

      setPhotos([]);
      setSignatureDataUrl(null);
      setSignatureNonce((n) => n + 1);
      setJustSaved(true);
      onSubmitted();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Gagal menyimpan kunjungan. Coba lagi.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {justSaved && (
        <div className="rounded-[var(--radius-md)] border border-status-green/30 bg-status-green-bg p-3 text-sm text-status-green">
          Kunjungan tersimpan.
        </div>
      )}

      <div className="flex flex-col gap-1">
        <label className="text-xs font-semibold text-ink-muted" htmlFor="visit-date">
          Tanggal Kunjungan
        </label>
        <input
          id="visit-date"
          type="date"
          value={values.date}
          onChange={(e) => updateField("date", e.target.value)}
          disabled={submitting}
          className="h-10 rounded-[var(--radius-md)] border border-border bg-surface px-3 text-sm text-ink outline-none focus:border-brand-primary"
        />
      </div>

      <Textarea
        id="visit-notes"
        label="Catatan Kunjungan"
        value={values.notes}
        onChange={(e) => updateField("notes", e.target.value)}
        disabled={submitting}
        placeholder="Kondisi siswa, progres kompetensi, catatan dari pembimbing industri..."
        rows={4}
        maxLength={MAX_NOTES}
      />

      <PhotoUploader
        max={1}
        photos={photos}
        setPhotos={setPhotos}
        disabled={submitting}
        uploadUrlPath={`/placements/${placementId}/visits/upload-url`}
      />

      <SignaturePad key={signatureNonce} onChange={setSignatureDataUrl} disabled={submitting} />

      {error && <p className="text-sm text-status-red">{error}</p>}

      <Button size="lg" className="w-full" onClick={handleSubmit} loading={submitting} disabled={!canSubmit}>
        Simpan Kunjungan
      </Button>
    </div>
  );
}
