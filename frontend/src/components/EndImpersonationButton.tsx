"use client";

import { useState } from "react";
import { Button } from "@/components/ui";

/** VOK-H6-E3 §2 — pasangan client ImpersonationBanner. Navigasi PENUH pasca-sukses (bukan router.push), alasan sama StartImpersonation: cookie httpOnly berubah, RSC cache lama harus dibuang. */
export function EndImpersonationButton() {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleEnd() {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/sa/impersonate/end", { method: "POST" });
      const body = await res.json();
      if (!res.ok) {
        throw new Error(body.message ?? "Gagal mengakhiri impersonasi.");
      }
      window.location.href = body.redirectTo;
    } catch (err) {
      setError(err instanceof Error ? err.message : "Gagal mengakhiri impersonasi.");
      setLoading(false);
    }
  }

  return (
    <div className="flex items-center gap-2">
      {error && <span className="text-xs text-status-red">{error}</span>}
      <Button variant="secondary" size="md" loading={loading} onClick={handleEnd} className="px-3 text-xs">
        Kembali ke akun SuperAdmin
      </Button>
    </div>
  );
}
