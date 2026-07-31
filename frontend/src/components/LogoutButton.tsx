import { Button } from "@/components/ui";

/**
 * VOK-H2-E2 §components/LogoutButton.tsx — POST /api/auth/logout (BFF, H2-E3) -> redirect /login.
 * Form HTML biasa (bukan onClick+fetch): jalan tanpa JS, konsisten dgn "Server Components default"
 * (AGENTS.md #10) — tidak butuh "use client" sama sekali.
 */
export function LogoutButton() {
  return (
    <form action="/api/auth/logout" method="POST">
      <Button type="submit" variant="danger-outline" size="md">
        Keluar
      </Button>
    </form>
  );
}
