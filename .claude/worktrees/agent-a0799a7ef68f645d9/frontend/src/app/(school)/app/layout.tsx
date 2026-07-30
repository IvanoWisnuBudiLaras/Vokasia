import type { ReactNode } from "react";
import { LogoutButton } from "@/components/LogoutButton";
import { NotificationBell } from "@/components/NotificationBell";
import { SidebarNav } from "@/components/SidebarNav";

/**
 * Shell desktop-first Admin Sekolah/Guru (PRD W3). Nav dipindah ke SidebarNav.tsx (VOK-H2-E3
 * hallmark-flow, DECISIONS.md D19) — SchoolLayout SENDIRI tetap Server Component (tidak perlu
 * "use client" krn tidak lagi menyimpan state/pathname di sini).
 *
 * Nav archetype: side-rail persisten (dekat N3 hallmark component-cookbook.md), BUKAN N1b yang
 * jadi default genre modern-minimal di tabel routing hallmark — deviasi sengaja: tabel routing
 * itu dikalibrasi utk halaman marketing, sedangkan ini app internal terautentikasi dgn 8 tujuan
 * persisten (Dashboard/Periode/Siswa/DUDI/Placement/Jurnal/Penilaian/Billing); side-rail adalah
 * pola mapan utk kasus ini (Linear/Vercel/Stripe dashboard), N1b akan penuh sesak/butuh dropdown
 * di 8 item. Lihat D19 utk detail lengkap.
 */
export default function SchoolLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-56 shrink-0 flex-col border-r border-border bg-surface-muted p-4 md:flex">
        <div className="mb-6 flex items-center justify-between">
          <span className="text-lg font-semibold text-ink">Vokasia</span>
          <NotificationBell />
        </div>
        <SidebarNav />
        <div className="mt-auto pt-4">
          <LogoutButton />
        </div>
      </aside>
      <main className="flex-1 p-6">{children}</main>
    </div>
  );
}
