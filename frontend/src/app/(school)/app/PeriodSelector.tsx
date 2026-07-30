"use client";

import { useRouter } from "next/navigation";
import type { PeriodSummary } from "@/lib/apiTypes";

export interface PeriodSelectorProps {
  periods: PeriodSummary[];
  value: string;
}

/**
 * VOK-H4-E2 §1 PeriodSelector — ganti periode aktif dashboard, persist di URL query (?periodId=)
 * supaya refresh/share-link tetap di periode yang sama (bukan state client murni yang hilang).
 */
export function PeriodSelector({ periods, value }: PeriodSelectorProps) {
  const router = useRouter();

  if (periods.length === 0) return null;

  return (
    <label className="flex items-center gap-2 text-sm text-ink-muted">
      Periode
      <select
        value={value}
        onChange={(e) => router.push(`/app?periodId=${e.target.value}`)}
        className={
          "h-[var(--tap-min)] rounded-[var(--radius-md)] border border-border bg-surface px-2 text-sm text-ink outline-none " +
          "focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
        }
      >
        {periods.map((p) => (
          <option key={p.id} value={p.id}>
            {p.name}
          </option>
        ))}
      </select>
    </label>
  );
}
