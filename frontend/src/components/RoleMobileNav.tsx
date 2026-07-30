"use client";

import { usePathname } from "next/navigation";
import { Icon, type IconName } from "@/components/ui";

export interface RoleMobileNavItem {
  href: string;
  label: string;
  icon: IconName;
}

interface RoleMobileNavProps {
  items: RoleMobileNavItem[];
  /** Sembunyikan bottom nav hanya saat sidebar desktop sudah tampil. */
  hideAtDesktop?: boolean;
}

function isCurrent(pathname: string | null, href: string): boolean {
  if (!pathname) return false;
  return ["/student", "/mentor", "/app", "/sa"].includes(href) ? pathname === href : pathname.startsWith(href);
}

/** Navigasi tugas mobile: status aktif eksplisit tanpa menambah state/data client lain. */
export function RoleMobileNav({ items, hideAtDesktop = false }: RoleMobileNavProps) {
  const pathname = usePathname();
  const scrollable = items.length > 4;

  return (
    <nav
      aria-label="Navigasi utama"
      className={
        "fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface/95 px-2 pb-[env(safe-area-inset-bottom)] backdrop-blur " +
        (hideAtDesktop ? "lg:hidden" : "")
      }
    >
      <div className={`mx-auto flex max-w-lg ${scrollable ? "overflow-x-auto overscroll-x-contain" : ""}`}>
        {items.map((item) => {
          const active = isCurrent(pathname, item.href);
          return (
            <a
              key={item.href}
              href={item.href}
              aria-current={active ? "page" : undefined}
              className={
                "relative flex min-h-[var(--tap-min)] flex-col items-center justify-center gap-0.5 rounded-[var(--radius-md)] px-2 py-1 text-xs font-medium outline-none transition-[color,background-color,border-color] duration-[var(--dur-fast)] " +
                "focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-[-2px] active:translate-y-px " +
                (scrollable ? "min-w-20 flex-none " : "flex-1 ") +
                (active ? "bg-primary-muted text-primary" : "text-ink-muted hover:bg-surface-muted hover:text-ink")
              }
            >
              <Icon name={item.icon} size={20} />
              <span className="whitespace-nowrap">{item.label}</span>
              {active && <span aria-hidden="true" className="absolute inset-x-5 bottom-0 h-0.5 rounded-full bg-primary" />}
            </a>
          );
        })}
      </div>
    </nav>
  );
}
