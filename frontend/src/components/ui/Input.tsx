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
          "h-[var(--tap-min)] rounded-[var(--radius-md)] border bg-surface px-3 text-base text-ink outline-none transition-[color,background-color,border-color] placeholder:text-ink-muted/80 disabled:cursor-not-allowed disabled:bg-surface-muted disabled:text-ink-muted disabled:opacity-[0.55]",
          "hover:border-primary/50 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1",
          error ? "border-status-red" : "border-border",
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
