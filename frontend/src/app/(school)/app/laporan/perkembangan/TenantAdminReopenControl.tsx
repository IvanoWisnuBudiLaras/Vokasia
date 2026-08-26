"use client";

import { useState, type FormEvent } from "react";
import type { LearningAssessmentStage } from "@/lib/apiTypes";
import { ApiError, apiClient } from "@/lib/apiClient";
import { Button, Textarea } from "@/components/ui";

export function canSubmitReopenReason(reason: string): boolean {
  return reason.trim().length > 0;
}

export function TenantAdminReopenControl({ placementId, stage }: { placementId: string; stage: LearningAssessmentStage }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!canSubmitReopenReason(reason)) return;
    setBusy(true);
    setError(null);
    try {
      await apiClient.post(`/placements/${placementId}/learning-assessments/${stage}/reopen`, { reason: reason.trim() });
      window.location.reload();
    } catch (cause) {
      setBusy(false);
      setError(cause instanceof ApiError ? cause.message : "Assessment belum bisa dibuka kembali.");
    }
  };

  if (!open) {
    return <Button type="button" variant="secondary" size="md" onClick={() => setOpen(true)}>Buka kembali {stage}</Button>;
  }

  return (
    <form onSubmit={submit} className="flex min-w-[min(100%,22rem)] flex-col gap-3 rounded-[var(--radius-md)] border border-status-amber/40 bg-status-amber/5 p-3 sm:min-w-[26rem]" aria-label={`Buka kembali assessment ${stage}`}>
      <Textarea label="Alasan reopen" maxLength={1000} value={reason} error={error ?? undefined} onChange={(event) => setReason(event.target.value)} />
      <div className="flex flex-wrap justify-end gap-2">
        <Button type="button" variant="secondary" size="md" onClick={() => { setOpen(false); setReason(""); setError(null); }}>Batal</Button>
        <Button type="submit" size="md" loading={busy} disabled={!canSubmitReopenReason(reason)}>Konfirmasi reopen</Button>
      </div>
    </form>
  );
}
