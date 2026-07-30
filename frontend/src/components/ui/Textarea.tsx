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
          "min-h-32 resize-y rounded-[var(--radius-md)] border bg-surface px-3 py-2 text-base text-ink outline-none transition-[color,background-color,border-color] placeholder:text-ink-muted/80 disabled:cursor-not-allowed disabled:bg-surface-muted disabled:text-ink-muted disabled:opacity-[0.55]",
          "hover:border-primary/50 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-1",
          error ? "border-status-red" : "border-border",
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
