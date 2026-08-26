import { cn } from "@/lib/cn";
import { useId, type TextareaHTMLAttributes } from "react";

export interface TextareaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label: string;
  maxLength: number;
  showCounter?: boolean;
  error?: string;
}

/** Textarea dengan counter — dipakai form jurnal (teks <=500 kar, FR-JRN-02). */
export function Textarea({
  label,
  maxLength,
  showCounter = true,
  error,
  id,
  value,
  className,
  ...rest
}: TextareaProps) {
  const autoId = useId();
  const textareaId = id ?? autoId;
  const length = typeof value === "string" ? value.length : 0;
  const nearLimit = length >= maxLength * 0.9;

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center justify-between">
        <label htmlFor={textareaId} className="text-sm font-medium text-ink">
          {label}
        </label>
        {showCounter && (
          <span className={cn("text-xs", nearLimit ? "text-status-amber" : "text-ink-muted")}>
            {length}/{maxLength}
          </span>
        )}
      </div>
      <textarea
        id={textareaId}
        maxLength={maxLength}
        value={value}
        aria-invalid={!!error}
        aria-describedby={error ? `${textareaId}-error` : undefined}
        className={cn(
          "min-h-32 resize-y rounded-lg border bg-surface-paper/50 px-3 py-3 text-base text-ink outline-none transition-all duration-[var(--dur-fast)] placeholder:text-ink-muted/60 disabled:cursor-not-allowed disabled:bg-surface-muted disabled:opacity-[0.55]",
          "hover:bg-surface hover:border-brand-accent/50 focus:bg-surface focus:border-brand-action focus:ring-2 focus:ring-brand-action/20",
          error ? "border-status-red focus:border-status-red focus:ring-status-red/20" : "border-border/60",
          className
        )}
        {...rest}
      />
      <div className="min-h-[1.25rem] text-xs">
        {error && (
          <span id={`${textareaId}-error`} className="text-status-red">
            {error}
          </span>
        )}
      </div>
    </div>
  );
}
