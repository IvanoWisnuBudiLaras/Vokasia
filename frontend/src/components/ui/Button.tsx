import { cn } from "@/lib/cn";
import type { ButtonHTMLAttributes } from "react";

type Variant = "primary" | "secondary" | "danger";
type Size = "md" | "lg";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
}

const variantClass: Record<Variant, string> = {
  primary: "bg-primary text-primary-ink hover:opacity-90",
  secondary: "bg-surface-muted text-ink border border-border hover:bg-border/40",
  danger: "bg-status-red text-white hover:opacity-90",
};

// lg = target sentuh utama mobile (NFR-UX-02, tombol "KIRIM JURNAL" / "APPROVE").
const sizeClass: Record<Size, string> = {
  md: "h-10 px-4 text-sm",
  lg: "h-[var(--tap-min)] px-6 text-base",
};

/** Tombol seragam satu-satunya di seluruh app — jangan buat tombol custom di luar ini. */
export function Button({
  variant = "primary",
  size = "md",
  loading = false,
  disabled,
  className,
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      className={cn(
        "inline-flex items-center justify-center gap-2 rounded-[var(--radius-md)] font-medium transition-opacity disabled:opacity-50 disabled:cursor-not-allowed",
        variantClass[variant],
        sizeClass[size],
        className
      )}
      disabled={disabled || loading}
      aria-busy={loading}
      {...rest}
    >
      {loading && (
        <span
          className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent"
          aria-hidden="true"
        />
      )}
      {children}
    </button>
  );
}
