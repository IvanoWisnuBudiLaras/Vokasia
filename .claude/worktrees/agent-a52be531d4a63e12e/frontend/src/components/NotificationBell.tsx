"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { apiClient } from "@/lib/apiClient";
import type { NotificationDto, Paged } from "@/lib/apiTypes";
import { NotificationPanel } from "./NotificationPanel";

const POLL_INTERVAL_MS = 60_000;

/**
 * VOK-H4-E2 §3 NotificationBell() — ikon+badge unread (poll ringan 60 dtk, AC), klik -> panel.
 * Dipasang di SEMUA 4 shell (school/mentor/student/sa) — `NotificationEndpoints` backend memang
 * `.RequireAuthorization()` BARE (lintas peran, lihat doc-comment endpoint asli), jadi 1 komponen
 * generik ini bisa dipakai apa adanya di layout mana pun tanpa cabang per-role.
 *
 * Badge dihitung dari `items.filter(!isRead)` pada 1 halaman pertama (pageSize=20) — BUKAN total
 * unread sesungguhnya lintas semua halaman (disederhanakan sengaja, "poll ringan" — memanggil
 * `unreadOnly=true` terpisah tiap 60 dtk cuma demi angka pasti dianggap kurang sepadan drpd 1
 * panggilan gabungan; dicatat sbg simplifikasi, bukan bug, di DECISIONS.md).
 */
export function NotificationBell() {
  const [items, setItems] = useState<NotificationDto[]>([]);
  const [open, setOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const load = useCallback(async () => {
    try {
      const paged = await apiClient.get<Paged<NotificationDto>>("/notifications?pageSize=20");
      setItems(paged.items);
    } catch {
      // Poll senyap gagal - jangan ganggu UI dgn error state utk widget non-kritis ini; percobaan
      // berikutnya (60 dtk lagi) akan coba lagi otomatis.
    }
  }, []);

  useEffect(() => {
    load();
    const id = setInterval(load, POLL_INTERVAL_MS);
    return () => clearInterval(id);
  }, [load]);

  useEffect(() => {
    if (!open) return;
    function onClickOutside(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, [open]);

  async function markRead(id: string) {
    setItems((prev) => prev.map((n) => (n.id === id ? { ...n, isRead: true } : n))); // optimistic.
    try {
      await apiClient.post(`/notifications/${id}/read`);
    } catch {
      load(); // gagal - sinkronkan ulang dari server drpd biarkan state client menyimpang diam-diam.
    }
  }

  async function markAllRead() {
    setItems((prev) => prev.map((n) => ({ ...n, isRead: true })));
    try {
      await apiClient.post("/notifications/read-all");
    } catch {
      load();
    }
  }

  const unreadCount = items.filter((n) => !n.isRead).length;

  return (
    <div ref={containerRef} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-label={`Notifikasi${unreadCount > 0 ? ` (${unreadCount} belum dibaca)` : ""}`}
        aria-expanded={open}
        className={
          "relative flex h-10 w-10 items-center justify-center rounded-full text-lg outline-none transition-colors " +
          "hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
        }
      >
        <span aria-hidden="true">🔔</span>
        {unreadCount > 0 && (
          <span className="absolute right-1 top-1 flex h-4 min-w-4 items-center justify-center rounded-full bg-status-red px-1 text-[10px] font-semibold text-white">
            {unreadCount > 9 ? "9+" : unreadCount}
          </span>
        )}
      </button>

      {open && <NotificationPanel items={items} onMarkRead={markRead} onMarkAllRead={markAllRead} />}
    </div>
  );
}
