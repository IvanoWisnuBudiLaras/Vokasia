import type { ReactNode } from "react";

export interface EmptyStateProps {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
}

/** Layar tanpa data TIDAK PERNAH kosong buntu (NFR-UX-04) — dipakai di semua list/halaman. */
export function EmptyState({ icon, title, description, action }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center gap-2 rounded-[var(--radius-lg)] border border-dashed border-border p-8 text-center">
      {icon && <div className="text-3xl" aria-hidden="true">{icon}</div>}
      <p className="text-sm font-medium text-ink">{title}</p>
      {description && <p className="text-sm text-ink-muted">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  );
}
