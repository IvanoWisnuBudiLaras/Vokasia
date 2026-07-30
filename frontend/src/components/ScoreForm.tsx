"use client";

import { useEffect, useRef, useState } from "react";
import { Icon } from "@/components/ui";
import { ApiError } from "@/lib/apiClient";

export interface ScoreAspectInput {
  id: string;
  name: string;
  kind: number;
  weight: number;
}

export interface ScoreFormProps {
  aspects: ScoreAspectInput[];
  values: Record<string, number | null>;
  onSave: (aspectId: string, value: number) => Promise<void>;
  readOnly?: boolean;
}

type RowStatus = "idle" | "saving" | "saved" | "error";

const AUTOSAVE_DEBOUNCE_MS = 800;

/**
 * VOK-H5-E2 §2 ScoreForm({aspects, values, onSave, readOnly}) — dipakai MENTOR
 * (mentor/nilai/[placementId]) & GURU (app/penilaian, sisi guru) sekaligus, beda hanya
 * `onSave` yang diteruskan pemanggil (SubmitMentorScores vs SubmitTeacherScores).
 *
 * "Autosave draft" (AC: "isi 3 aspek lalu tutup, kembali -> draft tersisa") SENGAJA diimplementasi
 * sbg debounce 800ms lalu PANGGIL BENERAN endpoint Submit*Scores per-aspek yang diubah (bukan
 * localStorage/state lokal murni) - assessment draft (IsFinal=false) DI BACKEND SUDAH MEMANG
 * "draft yang tersimpan", jadi draft yang persist antar sesi otomatis benar tanpa state
 * tambahan apa pun: reload halaman -> GetAssessment kembalikan nilai yang sudah tersubmit.
 * Ini juga architecturally konsisten dgn aturan proyek (BFF/token tak pernah localStorage,
 * backend selalu single source of truth).
 *
 * `readOnly` (assessment.IsFinal) -> semua input disabled, tak ada percobaan save (AC: "admin
 * finalize sukses -> semua ScoreForm jadi readOnly").
 */
export function ScoreForm({ aspects, values, onSave, readOnly = false }: ScoreFormProps) {
  const [local, setLocal] = useState<Record<string, number>>(() => {
    const init: Record<string, number> = {};
    for (const a of aspects) init[a.id] = values[a.id] ?? 0;
    return init;
  });
  const [status, setStatus] = useState<Record<string, RowStatus>>({});
  const [errorMsg, setErrorMsg] = useState<Record<string, string>>({});
  const timers = useRef<Record<string, ReturnType<typeof setTimeout>>>({});

  useEffect(() => {
    const pendingTimers = timers.current;
    return () => {
      Object.values(pendingTimers).forEach(clearTimeout);
    };
  }, []);

  function scheduleSave(aspectId: string, value: number) {
    if (timers.current[aspectId]) clearTimeout(timers.current[aspectId]);
    setStatus((s) => ({ ...s, [aspectId]: "saving" }));
    timers.current[aspectId] = setTimeout(async () => {
      try {
        await onSave(aspectId, value);
        setStatus((s) => ({ ...s, [aspectId]: "saved" }));
        setErrorMsg((e) => ({ ...e, [aspectId]: "" }));
      } catch (err) {
        setStatus((s) => ({ ...s, [aspectId]: "error" }));
        setErrorMsg((e) => ({ ...e, [aspectId]: err instanceof ApiError ? err.message : "Gagal menyimpan skor." }));
      }
    }, AUTOSAVE_DEBOUNCE_MS);
  }

  function handleChange(aspectId: string, raw: string) {
    const value = Math.max(0, Math.min(100, Number(raw) || 0));
    setLocal((v) => ({ ...v, [aspectId]: value }));
    if (!readOnly) scheduleSave(aspectId, value);
  }

  return (
    <div className="flex flex-col gap-4">
      {aspects.map((aspect) => {
        const rowStatus = status[aspect.id] ?? "idle";
        return (
          <div key={aspect.id} className="rounded-[var(--radius-lg)] border border-border bg-surface p-3">
            <div className="mb-2 flex items-center justify-between gap-2">
              <span className="text-sm font-medium text-ink">{aspect.name}</span>
              <span className="text-xs text-ink-muted">bobot {aspect.weight}%</span>
            </div>
            <div className="flex items-center gap-3">
              <input
                type="range"
                min={0}
                max={100}
                step={1}
                value={local[aspect.id] ?? 0}
                disabled={readOnly}
                onChange={(e) => handleChange(aspect.id, e.target.value)}
                className="h-[var(--tap-min)] flex-1 cursor-pointer accent-primary outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1 disabled:cursor-not-allowed disabled:opacity-[0.55]"
                aria-label={`Skor ${aspect.name}`}
                aria-describedby={`${aspect.id}-save-status`}
              />
              <input
                type="number"
                min={0}
                max={100}
                value={local[aspect.id] ?? 0}
                disabled={readOnly}
                onChange={(e) => handleChange(aspect.id, e.target.value)}
                aria-label={`Nilai angka ${aspect.name}`}
                aria-describedby={`${aspect.id}-save-status`}
                className="h-[var(--tap-min)] w-20 rounded-[var(--radius-md)] border border-border px-2 text-center text-base outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1 disabled:cursor-not-allowed disabled:bg-surface-muted disabled:opacity-[0.55]"
              />
            </div>
            <div
              id={`${aspect.id}-save-status`}
              role={rowStatus === "error" ? "alert" : "status"}
              aria-live="polite"
              className="mt-1 min-h-[1rem] text-xs"
            >
              {rowStatus === "saving" && <span className="text-ink-muted">Menyimpan…</span>}
              {rowStatus === "saved" && (
                <span className="inline-flex items-center gap-1 text-status-green">
                  Tersimpan <Icon name="check" size={16} />
                </span>
              )}
              {rowStatus === "error" && <span className="text-status-red">{errorMsg[aspect.id] || "Gagal menyimpan."}</span>}
            </div>
          </div>
        );
      })}
    </div>
  );
}
