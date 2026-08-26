import { cn } from "@/lib/cn";
import type { ButtonHTMLAttributes } from "react";

type Variant = "primary" | "secondary" | "danger" | "danger-outline";
type Size = "md" | "lg";

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
}

const variantClass: Record<Variant, string> = {
  primary: "bg-primary text-white shadow-[0_2px_4px_0_oklch(50.4%_0.162_243.3/0.25)] hover:bg-brand-strong hover:-translate-y-0.5 active:translate-y-0 active:bg-brand-strong/90 transition-all duration-[var(--dur-fast)] font-semibold",
  secondary: "border border-border/60 bg-surface text-ink hover:bg-surface-muted hover:text-ink active:bg-surface-muted/70 transition-colors font-medium",
  danger: "bg-status-red text-white shadow-sm hover:bg-status-red/90 active:bg-status-red/80 font-semibold",
  "danger-outline": "border border-status-red/30 bg-transparent text-status-red hover:bg-status-red/5 active:bg-status-red/10 transition-colors font-medium",
};

// lg = target sentuh utama mobile (NFR-UX-02, tombol "KIRIM JURNAL" / "APPROVE").
const sizeClass: Record<Size, string> = {
  md: "h-[var(--tap-min)] px-4 text-base",
  lg: "h-[var(--tap-min)] px-6 text-base",
};

/**
 * Tombol seragam satu-satunya di seluruh app — jangan buat tombol custom di luar ini.
 *
 * Celah ditemukan+ditambal sesi hallmark-flow (DECISIONS.md D19): sebelumnya TANPA focus-visible
 * sama sekali (bergantung ke outline default browser, yang di banyak browser nyaris tak
 * kelihatan pada tombol berwarna) — dgn Button dipakai di puluhan tempat (termasuk retry
 * ErrorState & LogoutButton), ini satu perbaikan yang otomatis menjalar ke seluruh app. Pakai
 * --color-focus (token ada sejak D18, sebelumnya tak dipakai satu komponen pun).
 */
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
        "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] font-medium outline-none transition-transform duration-[var(--dur-fast)] disabled:cursor-not-allowed disabled:opacity-50 active:translate-y-px",
        "focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2",
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
