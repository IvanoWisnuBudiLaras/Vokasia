"use client";

import { useRef, useState, type Dispatch, type SetStateAction } from "react";
import { Icon } from "@/components/ui";
import { apiClient } from "@/lib/apiClient";
import type { PresignedUploadDto } from "@/lib/apiTypes";

export interface PendingPhoto {
  localId: string;
  file: File;
  previewUrl: string;
  status: "uploading" | "uploaded" | "error";
  objectKey?: string;
  errorMessage?: string;
}

interface PhotoUploaderProps {
  max: number;
  photos: PendingPhoto[];
  setPhotos: Dispatch<SetStateAction<PendingPhoto[]>>;
  disabled?: boolean;
  /**
   * VOK-H5-E2: path presign dibuat bisa diganti (default tetap jurnal, jadi seluruh caller lama
   * di (student)/student/* TIDAK berubah perilaku) — dipakai jg oleh VisitForm.tsx
   * (app/(school)/app/bimbingan/[placementId]/kunjungan/) via "/placements/{id}/visits/upload-url"
   * (endpoint presign BARU, lihat DECISIONS.md D34 - Teacher tak py akses ke "/journals/upload-url"
   * yg dikunci StudentSelf). Object key & bucket-nya beda (visit-photo/ vs journal/) tp bentuk
   * request/response (UploadRequest/PresignedUploadDto) SAMA PERSIS, jadi komponen ini reusable
   * apa adanya tanpa logic baru, cukup ganti path.
   */
  uploadUrlPath?: string;
}

const ALLOWED_TYPES = ["image/jpeg", "image/png", "image/webp"];
const MAX_SIZE_BYTES = 5 * 1024 * 1024;

/**
 * VOK-H3-E2 §1 PhotoUploader. Alur presigned: minta URL (GetPresignedUploadUrl) -> PUT langsung ke
 * MinIO -> simpan objectKey di state lokal. Preview thumbnail lokal via blob URL, progress+error
 * per file (retry tanpa membatalkan form/foto lain), batal per file.
 *
 * [TEMUAN dicatat, bukan diam-diam - lihat DECISIONS.md]: AttachPhoto (backend H3-E1,
 * Vokasia.Api/Endpoints/JournalEndpoints.cs) butuh JournalEntry yang SUDAH ADA (route
 * /api/journals/{id}/photos, {id} = entry id) - tapi entry itu baru dibuat SubmitJournal, yang
 * baru dipanggil siswa SETELAH memilih foto (sesuai urutan wireframe W1: foto dipilih SEBELUM
 * tombol KIRIM JURNAL ditekan). Maka AttachPhoto TIDAK dipanggil dari komponen ini - hanya
 * presign+PUT MinIO yang terjadi di sini (tak butuh entry id sama sekali, cukup tenant siswa);
 * JournalForm.tsx-lah yang memanggil AttachPhoto per foto SETELAH SubmitJournal sukses &
 * mengembalikan entry.id (lihat handleSubmit di sana) - urutan real yang tetap valid tanpa
 * mengubah backend sama sekali (di luar wilayah ticket ini, `frontend/` saja).
 */
export function PhotoUploader({ max, photos, setPhotos, disabled, uploadUrlPath = "/journals/upload-url" }: PhotoUploaderProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [pickError, setPickError] = useState<string | null>(null);

  async function uploadFile(file: File, localId: string) {
    try {
      const presigned = await apiClient.post<PresignedUploadDto>(uploadUrlPath, {
        fileName: file.name,
        contentType: file.type,
        sizeBytes: file.size,
      });

      const putRes = await fetch(presigned.uploadUrl, {
        method: "PUT",
        headers: { "Content-Type": file.type },
        body: file,
      });
      if (!putRes.ok) {
        throw new Error(`Unggah ke penyimpanan gagal (${putRes.status}).`);
      }

      setPhotos((prev) =>
        prev.map((p) => (p.localId === localId ? { ...p, status: "uploaded" as const, objectKey: presigned.objectKey } : p))
      );
    } catch (err) {
      setPhotos((prev) =>
        prev.map((p) =>
          p.localId === localId
            ? { ...p, status: "error" as const, errorMessage: err instanceof Error ? err.message : "Gagal unggah foto." }
            : p
        )
      );
    }
  }

  function handlePick(fileList: FileList | null) {
    setPickError(null);
    if (!fileList || fileList.length === 0) return;

    const remaining = max - photos.length;
    const files = Array.from(fileList).slice(0, remaining);
    if (fileList.length > remaining) {
      setPickError(`Maksimal ${max} foto per jurnal.`);
    }

    for (const file of files) {
      if (!ALLOWED_TYPES.includes(file.type)) {
        setPickError("Hanya foto JPEG, PNG, atau WEBP yang didukung.");
        continue;
      }
      if (file.size > MAX_SIZE_BYTES) {
        setPickError("Ukuran foto maksimal 5MB.");
        continue;
      }

      const localId = crypto.randomUUID();
      const previewUrl = URL.createObjectURL(file);
      setPhotos((prev) => [...prev, { localId, file, previewUrl, status: "uploading" as const }]);
      void uploadFile(file, localId);
    }

    if (inputRef.current) inputRef.current.value = "";
  }

  function removePhoto(localId: string) {
    const target = photos.find((photo) => photo.localId === localId);
    if (target) URL.revokeObjectURL(target.previewUrl);
    setPhotos((prev) => prev.filter((p) => p.localId !== localId));
  }

  function retryPhoto(localId: string) {
    const target = photos.find((p) => p.localId === localId);
    if (!target) return;
    setPhotos((prev) =>
      prev.map((p) => (p.localId === localId ? { ...p, status: "uploading" as const, errorMessage: undefined } : p))
    );
    void uploadFile(target.file, localId);
  }

  return (
    <div className="flex flex-col gap-1.5">
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-ink">Foto</span>
        <span className="text-xs text-ink-muted">
          {photos.length}/{max}
        </span>
      </div>

      <div className="flex flex-wrap gap-2">
        {photos.map((p) => (
          <div key={p.localId} className="relative h-20 w-20 overflow-hidden rounded-[var(--radius-md)] border border-border">
            {/* Preview blob URL lokal, bukan aset remote - <img> biasa, bukan next/image. */}
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={p.previewUrl} alt={`Pratinjau ${p.file.name}`} className="h-full w-full object-cover" />
            {p.status === "uploading" && (
              <div className="absolute inset-0 flex items-center justify-center bg-ink/40">
                <span
                  className="h-5 w-5 animate-spin rounded-full border-2 border-primary-ink border-t-transparent"
                  aria-hidden="true"
                />
              </div>
            )}
            {p.status === "error" && (
              <button
                type="button"
                onClick={() => retryPhoto(p.localId)}
                title={p.errorMessage}
                aria-label={`Unggah ulang ${p.file.name}`}
                className="absolute inset-0 flex flex-col items-center justify-center gap-0.5 bg-status-red-bg text-[10px] font-medium text-status-red outline-none transition-[color,background-color,border-color] hover:bg-status-red-bg/80 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-[-2px] active:bg-status-red-bg/60"
              >
                <Icon name="warning" size={16} />
                Ulangi
              </button>
            )}
            <button
              type="button"
              onClick={() => removePhoto(p.localId)}
              aria-label="Hapus foto ini"
              className="absolute right-0 top-0 flex min-h-[var(--tap-min)] min-w-[var(--tap-min)] items-center justify-center rounded-bl-[var(--radius-md)] bg-ink/70 text-primary-ink outline-none transition-[color,background-color,border-color] hover:bg-ink/80 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-[-2px] active:bg-ink/90"
            >
              <Icon name="x" size={16} />
            </button>
          </div>
        ))}

        {photos.length < max && (
          <button
            type="button"
            disabled={disabled}
            onClick={() => inputRef.current?.click()}
            className="flex h-20 w-20 flex-col items-center justify-center gap-1 rounded-[var(--radius-md)] border border-dashed border-border text-ink-muted outline-none transition-[color,background-color,border-color] hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-transparent disabled:active:bg-transparent"
          >
            <Icon name="camera" size={20} />
            <span className="text-[11px]">Tambah foto</span>
          </button>
        )}
      </div>

      <input
        ref={inputRef}
        type="file"
        accept="image/jpeg,image/png,image/webp"
        multiple
        className="hidden"
        onChange={(e) => handlePick(e.target.files)}
      />

      {pickError && <span className="text-xs text-status-red">{pickError}</span>}
    </div>
  );
}
