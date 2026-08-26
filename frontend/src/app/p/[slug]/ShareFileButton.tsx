"use client";

import { useState } from "react";
import { Button } from "@/components/ui";

interface ShareFileButtonProps {
  url: string;
  filename: string;
  label: string;
  title: string;
}

export function ShareFileButton({ url, filename, label, title }: ShareFileButtonProps) {
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function shareFile() {
    setBusy(true);
    setMessage(null);
    try {
      const response = await fetch(url, { credentials: "same-origin" });
      if (!response.ok) throw new Error("file_unavailable");
      const blob = await response.blob();
      const file = new File([blob], filename, { type: blob.type || "application/octet-stream" });
      const canShareFiles = typeof navigator.canShare === "function" && navigator.canShare({ files: [file] });

      if (typeof navigator.share === "function" && canShareFiles) {
        await navigator.share({ title, files: [file] });
        setMessage("File siap dibagikan.");
        return;
      }

      const objectUrl = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = objectUrl;
      anchor.download = filename;
      anchor.click();
      URL.revokeObjectURL(objectUrl);
      setMessage("Perangkat belum mendukung berbagi file; unduhan dimulai.");
    } catch {
      setMessage("File belum dapat dibagikan. Coba unduh ulang.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <Button variant="secondary" size="md" onClick={shareFile} loading={busy}>{label}</Button>
      <span className="sr-only" role="status" aria-live="polite">{message}</span>
    </div>
  );
}
