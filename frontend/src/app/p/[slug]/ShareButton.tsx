"use client";

import { useState } from "react";
import { Button } from "@/components/ui";

export function ShareButton({ title }: { title: string }) {
  const [message, setMessage] = useState<string | null>(null);

  async function share() {
    const url = window.location.href;
    try {
      if (navigator.share) {
        await navigator.share({ title, url });
        setMessage("Tautan siap dibagikan.");
      } else {
        await navigator.clipboard.writeText(url);
        setMessage("Tautan disalin.");
      }
    } catch {
      setMessage("Tautan belum dibagikan.");
    }
  }

  return (
    <div className="flex flex-col items-end gap-1">
      <Button variant="secondary" size="md" onClick={share}>Bagikan</Button>
      <span className="sr-only" role="status" aria-live="polite">{message}</span>
    </div>
  );
}
