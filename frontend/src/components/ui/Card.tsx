import { cn } from "@/lib/cn";
import type { ReactNode } from "react";

export interface CardProps {
  title?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  className?: string;
}

/** Kontainer konten standar — satu-satunya "card" di seluruh app. */
export function Card({ title, children, footer, className }: CardProps) {
  return (
    <div
      className={cn(
        "rounded-[var(--radius-lg)] border border-border bg-surface p-4 shadow-sm transition-[color,background-color,border-color]",
        className
      )}
    >
      {title && <div className="mb-3 text-base font-semibold text-ink">{title}</div>}
      <div>{children}</div>
      {footer && <div className="mt-3 border-t border-border pt-3">{footer}</div>}
    </div>
  );
}
