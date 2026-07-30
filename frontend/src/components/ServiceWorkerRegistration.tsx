"use client";

import { useEffect } from "react";

/** Mendaftarkan fallback offline statis. Service worker tidak pernah meng-cache HTML privat/API. */
export function ServiceWorkerRegistration() {
  useEffect(() => {
    if ("serviceWorker" in navigator) {
      void navigator.serviceWorker.register("/sw.js", { scope: "/" });
    }
  }, []);

  return null;
}
