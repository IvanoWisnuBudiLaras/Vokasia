"use client";

import { usePathname } from "next/navigation";

export interface NavItem {
  href: string;
  label: string;
}

const NAV: NavItem[] = [
  { href: "/app", label: "Dashboard" },
  { href: "/app/bimbingan", label: "Bimbingan" },
  { href: "/app/periode", label: "Periode" },
  { href: "/app/siswa", label: "Siswa" },
  { href: "/app/dudi", label: "DUDI" },
  { href: "/app/placement", label: "Placement" },
  { href: "/app/jurnal", label: "Jurnal" },
  { href: "/app/penilaian", label: "Penilaian" },
  { href: "/app/billing", label: "Billing" },
];

/**
 * VOK-H2-E3 hallmark-flow (DECISIONS.md D19) — sebelumnya inline di layout.tsx dengan hanya
 * 2 dari 8 state interaktif (default+hover; lihat interaction-and-states.md hallmark: "most
 * AI-generated UI styles two and forgets the rest"). "use client" TERISOLASI di sini saja
 * (pola sama dgn OfflineBanner.tsx) krn usePathname butuh client — SchoolLayout di sekitarnya
 * tetap Server Component (AGENTS.md #10).
 *
 * State yang ditambahkan:
 * - Focus-visible: outline eksplisit pakai --color-focus (token ada sejak D18, sebelumnya
 *   TIDAK DIPAKAI oleh komponen mana pun — celah nyata, bukan kosmetik: tanpa ini navigasi
 *   keyboard-only tidak pernah melihat di mana fokus berada).
 * - Active/current page: aria-current="page" (channel non-visual, wajib) + bar aksen kiri
 *   (border-l-2, slot direservasi transparan di semua item lain -> ganti warna saja saat
 *   aktif, TANPA layout shift — "no-layout-shift rule" hallmark).
 * - Hover dipertahankan dari versi lama (bg+text), tidak diubah.
 *
 * Sengaja TIDAK menaikkan padding ke 44px tap target: /app desktop-first (DESIGN.md "Beku dari
 * PRD"), aturan sentuh 44px itu terikat NFR-UX-02 utk /student /mentor mobile-first.
 */
export function SidebarNav() {
  const pathname = usePathname();

  return (
    <nav className="flex flex-col gap-1">
      {NAV.map((item) => {
        const isActive = item.href === "/app" ? pathname === "/app" : pathname?.startsWith(item.href) ?? false;

        return (
          <a
            key={item.href}
            href={item.href}
            aria-current={isActive ? "page" : undefined}
            className={
              "relative flex items-center rounded-r-[var(--radius-sm)] border-l-2 px-3 py-2 text-sm outline-none " +
              "transition-colors duration-[var(--dur-fast)] ease-[var(--ease-out)] " +
              "focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 " +
              (isActive
                ? "border-primary bg-primary-muted font-medium text-primary"
                : "border-transparent text-ink-muted hover:bg-surface hover:text-ink")
            }
          >
            {item.label}
          </a>
        );
      })}
    </nav>
  );
}
