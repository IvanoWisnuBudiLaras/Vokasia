"use client";

import { Button } from "@/components/ui";

export function OfflineRetryButton() {
  return (
    <Button type="button" size="lg" className="mt-7" onClick={() => window.location.reload()}>
      Coba lagi
    </Button>
  );
}
