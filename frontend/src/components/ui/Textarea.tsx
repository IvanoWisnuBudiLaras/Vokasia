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
        className={cn(
          "min-h-32 rounded-[var(--radius-md)] border px-3 py-2 text-base outline-none resize-y",
          "focus:outline-2 focus:outline-primary focus:outline-offset-1",
          error ? "border-status-red" : "border-border",
          className
        )}
        {...rest}
      />
      {error && <span className="text-xs text-status-red">{error}</span>}
    </div>
  );
}
