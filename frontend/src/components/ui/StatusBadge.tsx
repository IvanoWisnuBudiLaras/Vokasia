import { cn } from "@/lib/cn";

export type RagStatus = "green" | "amber" | "red";

export interface StatusBadgeProps {
  status: RagStatus;
  label: string;
  className?: string;
}

const styleByStatus: Record<RagStatus, string> = {
  green: "bg-status-green-bg text-status-green",
  amber: "bg-status-amber-bg text-status-amber",
  red: "bg-status-red-bg text-status-red",
};

const dotByStatus: Record<RagStatus, string> = {
  green: "bg-status-green",
  amber: "bg-status-amber",
  red: "bg-status-red",
};

/** RAG badge konsisten lintas surface (dashboard W3, daftar approval W2). */
export function StatusBadge({ status, label, className }: StatusBadgeProps) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-medium",
        styleByStatus[status],
        className
      )}
    >
      <span aria-hidden="true" className={cn("h-2 w-2 shrink-0 rounded-full", dotByStatus[status])} />
      {label}
    </span>
  );
}
