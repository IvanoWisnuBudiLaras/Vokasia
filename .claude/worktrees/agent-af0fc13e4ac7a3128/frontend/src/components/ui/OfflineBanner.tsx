"use client";

import { useEffect, useState } from "react";

/**
 * Strip status koneksi (NFR-UX-04). Dipasang sekali di root layout — otomatis tampil/hilang
 * mengikuti navigator.onLine. Submit offline penuh (queue lokal) = fase 2, ini hanya notifikasi.
 */
export function OfflineBanner() {
  const [offline, setOffline] = useState(false);

  useEffect(() => {
    const update = () => setOffline(!navigator.onLine);
    update();
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => {
      window.removeEventListener("online", update);
      window.removeEventListener("offline", update);
    };
  }, []);

  if (!offline) return null;

  return (
    <div
      role="status"
      className="w-full bg-status-amber-bg px-4 py-2 text-center text-sm font-medium text-status-amber"
    >
      Kamu sedang offline. Sebagian fitur mungkin tidak berfungsi.
    </div>
  );
}
