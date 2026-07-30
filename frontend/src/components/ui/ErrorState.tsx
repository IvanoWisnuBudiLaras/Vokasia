"use client";

import { Button } from "./Button";

export interface ErrorStateProps {
  message?: string;
  onRetry?: () => void;
}

/** State error seragam — selalu dengan jalan keluar (retry), tidak pernah white screen. */
export function ErrorState({ message = "Terjadi kesalahan. Coba lagi.", onRetry }: ErrorStateProps) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center justify-center gap-3 rounded-[var(--radius-lg)] border border-status-red bg-status-red-bg p-8 text-center"
    >
      <p className="text-base font-medium text-status-red">{message}</p>
      <Button type="button" variant="secondary" onClick={onRetry ?? (() => window.location.reload())}>
        Coba Lagi
      </Button>
    </div>
  );
}
