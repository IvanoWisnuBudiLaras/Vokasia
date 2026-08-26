import { Icon } from "@/components/ui";
import { LogoutButton } from "@/components/LogoutButton";

/** Compact mobile account affordance; the full logout action remains in the opened menu. */
export function MobileAccountMenu() {
  return (
    <details className="relative">
      <summary className="flex min-h-[var(--tap-min)] cursor-pointer list-none items-center gap-1 rounded-[var(--radius-sm)] px-2 text-sm font-medium text-ink outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 [&::-webkit-details-marker]:hidden">
        Akun
        <Icon name="chevron-down" size={16} aria-hidden="true" />
      </summary>
      <div className="absolute right-0 top-full z-30 mt-2 rounded-[var(--radius-md)] border border-border bg-surface p-2 shadow-lg">
        <LogoutButton />
      </div>
    </details>
  );
}
