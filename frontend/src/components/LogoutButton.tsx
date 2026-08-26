"use client";

import { Button } from "@/components/ui";

/** Logout navigation clears the BFF session and lets the API emit Clear-Site-Data. */
export function LogoutButton() {
  return (
    <Button type="button" variant="danger-outline" size="md" onClick={() => window.location.assign("/api/auth/logout")}>
      Keluar
    </Button>
  );
}
