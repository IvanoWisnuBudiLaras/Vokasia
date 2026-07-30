"use client";

import { Button } from "./Button";

export function EmptyStateRefreshAction() {
  return (
    <Button type="button" variant="secondary" onClick={() => window.location.reload()}>
      Periksa lagi
    </Button>
  );
}
