"use client";

import { usePathname } from "next/navigation";
import { Icon, type IconName } from "@/components/ui";

export interface WorkspaceNavItem {
  href: string;
  label: string;
  icon: IconName;
}

interface WorkspaceSidebarProps {
  ariaLabel: string;
  items: WorkspaceNavItem[];
}

function isCurrent(pathname: string | null, href: string): boolean {
  if (!pathname) return false;
  return href === "/app" || href === "/sa" ? pathname === href : pathname.startsWith(href);
}

/** Daftar navigasi workspace desktop dengan state aktif dan fokus konsisten. */
export function WorkspaceSidebar({ ariaLabel, items }: WorkspaceSidebarProps) {
  const pathname = usePathname();

  return (
    <nav aria-label={ariaLabel} className="flex flex-col gap-1 px-2">
      {items.map((item) => {
        const active = isCurrent(pathname, item.href);
        return (
          <a
            key={item.href}
            href={item.href}
            aria-current={active ? "page" : undefined}
            className={
              "group flex min-h-[var(--tap-min)] items-center gap-3 rounded-lg px-3.5 py-2.5 text-sm transition-all duration-[var(--dur-fast)] " +
              "focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px " +
              (active
                ? "bg-brand-soft/70 font-semibold text-ink"
                : "text-ink-muted hover:bg-surface hover:text-ink")
            }
          >
            <Icon name={item.icon} size={20} className={active ? "text-ink" : "text-ink-muted group-hover:text-ink"} />
            <span>{item.label}</span>
          </a>
        );
      })}
    </nav>
  );
}
