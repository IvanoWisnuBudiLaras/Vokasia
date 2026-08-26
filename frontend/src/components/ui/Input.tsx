import { cn } from "@/lib/cn";
import { useId, type InputHTMLAttributes } from "react";

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  hint?: string;
}

/** Field berlabel dengan slot error ter-reservasi — mencegah layout shift saat error muncul. */
export function Input({ label, error, hint, id, className, ...rest }: InputProps) {
  const autoId = useId();
  const inputId = id ?? autoId;

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={inputId} className="text-sm font-medium text-ink">
        {label}
      </label>
      <input
        id={inputId}
        aria-invalid={!!error}
        aria-describedby={error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined}
        className={cn(
          "h-11 rounded-lg border bg-surface-paper/50 px-3 text-base text-ink outline-none transition-all duration-[var(--dur-fast)] placeholder:text-ink-muted/60 disabled:cursor-not-allowed disabled:bg-surface-muted disabled:opacity-[0.55]",
          "hover:bg-surface hover:border-brand-accent/50 focus:bg-surface focus:border-brand-action focus:ring-2 focus:ring-brand-action/20",
          error ? "border-status-red focus:border-status-red focus:ring-status-red/20" : "border-border/60",
          className
        )}
        {...rest}
      />
      <div className="min-h-[1.25rem] text-xs">
        {error ? (
          <span id={`${inputId}-error`} className="text-status-red">
            {error}
          </span>
        ) : hint ? (
          <span id={`${inputId}-hint`} className="text-ink-muted">
            {hint}
          </span>
        ) : null}
      </div>
    </div>
  );
}
