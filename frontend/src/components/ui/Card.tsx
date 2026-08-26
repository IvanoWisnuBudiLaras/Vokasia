import { cn } from "@/lib/cn";
import type { ReactNode } from "react";

export interface CardProps {
  title?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  className?: string;
  interactive?: boolean;
}

/** Kontainer konten standar — satu-satunya "card" di seluruh app. */
export function Card({ title, children, footer, className, interactive = false }: CardProps) {
  return (
    <div
      className={cn(
        "rounded-[var(--radius-lg)] border border-border/40 bg-surface p-5 transition-all duration-[var(--dur-base)]",
        interactive
          ? "shadow-[0_2px_8px_0_oklch(60.1%_0.165_243.3/0.03)] hover:shadow-[0_4px_12px_0_oklch(60.1%_0.165_243.3/0.06)] hover:-translate-y-0.5 active:translate-y-0 cursor-pointer"
          : "shadow-none",
        className
      )}
    >
      {title && <div className="mb-4 text-[17px] font-semibold tracking-tight text-ink">{title}</div>}
      <div className="text-base text-ink leading-relaxed">{children}</div>
      {footer && <div className="mt-5 border-t border-border/30 pt-4">{footer}</div>}
    </div>
  );
}
