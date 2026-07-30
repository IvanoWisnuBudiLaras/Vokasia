"use client";

import { useState } from "react";
import { Button, Textarea } from "@/components/ui";
import { apiClient, ApiError } from "@/lib/apiClient";
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
 * VOK-H5-E2 §1 VisitForm({placementId, onSubmitted}) — form W4 lengkap: tanggal (default hari ini,
 * editable), catatan, 1 foto lokasi (PhotoUploader reuse via uploadUrlPath baru, lihat D34), tanda
 * tangan (SignaturePad). AC: "isi kunjungan+ttd+foto -> tersimpan & muncul di riwayat; <=2 mnt" -
 * tombol besar (size lg, tap target >=44px), submit tunggal (bukan multi-step wizard).
 *
 * Tanda tangan TIDAK wajib (ticket tak eksplisit mewajibkan, `SignatureDataUrl` nullable di backend)
 * — guru boleh catat kunjungan tanpa tanda tangan pembimbing (mis. pembimbing sedang tak di tempat),
 * ditangkap belakangan di kunjungan berikutnya. Foto jg opsional (max 1, sama alasan).
 */
export function VisitForm({ placementId, onSubmitted }: VisitFormProps) {
  const [date, setDate] = useState(todayIso());
  const [notes, setNotes] = useState("");
  const [photos, setPhotos] = useState<PendingPhoto[]>([]);
  const [signatureDataUrl, setSignatureDataUrl] = useState<string | null>(null);
  const [signatureNonce, setSignatureNonce] = useState(0);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [justSaved, setJustSaved] = useState(false);

  const stillUploading = photos.some((p) => p.status === "uploading");
  const hasFailedPhoto = photos.some((p) => p.status === "error");
  const canSubmit = notes.trim().length > 0 && !submitting && !stillUploading;

  async function handleSubmit() {
    setError(null);
    setJustSaved(false);

    if (notes.trim().length === 0) {
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
        date,
        notes: notes.trim(),
        photoKey: photos[0]?.objectKey ?? null,
        signatureDataUrl,
      });

      // Reset form utk kunjungan berikutnya, tanggal tetap hari ini.
      setNotes("");
      setPhotos([]);
      setSignatureDataUrl(null);
      setSignatureNonce((n) => n + 1); // remount SignaturePad -> kanvas bersih.
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
        <label htmlFor="visit-date" className="text-sm font-medium text-ink">
          Tanggal kunjungan
        </label>
        <input
          id="visit-date"
          type="date"
          value={date}
          onChange={(e) => setDate(e.target.value)}
          disabled={submitting}
          className="h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border px-3 text-base text-ink outline-none focus:outline-2 focus:outline-primary focus:outline-offset-1"
        />
      </div>

      <Textarea
        label="Catatan kunjungan"
        maxLength={MAX_NOTES}
        showCounter
        value={notes}
        onChange={(e) => setNotes(e.target.value)}
        disabled={submitting}
        placeholder="Kondisi siswa, progres kompetensi, catatan dari pembimbing industri..."
      />

      <PhotoUploader max={1} photos={photos} setPhotos={setPhotos} disabled={submitting} uploadUrlPath={`/placements/${placementId}/visits/upload-url`} />

      <SignaturePad key={signatureNonce} onChange={setSignatureDataUrl} disabled={submitting} />

      {error && <p className="text-sm text-status-red">{error}</p>}

      <Button size="lg" className="w-full" onClick={handleSubmit} loading={submitting} disabled={!canSubmit}>
        Simpan Kunjungan
      </Button>
    </div>
  );
}
