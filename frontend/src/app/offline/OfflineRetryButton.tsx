"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui";

export function OfflineRetryButton() {
  const [checking, setChecking] = useState(false);

  const attemptRecovery = async () => {
    setChecking(true);
    try {
      const res = await fetch("/?_t=" + Date.now(), { cache: "no-store" });
      if (res.ok) {
        const params = new URLSearchParams(window.location.search);
        const target = params.get("from") || "/";
        window.location.href = target;
        return;
      }
    } catch {
      // Still offline
    } finally {
      setChecking(false);
    }
  };

  useEffect(() => {
    const handleOnline = () => {
      void attemptRecovery();
    };

    window.addEventListener("online", handleOnline);

    const timer = setInterval(() => {
      if (navigator.onLine && !checking) {
        void attemptRecovery();
      }
    }, 2500);

    return () => {
      window.removeEventListener("online", handleOnline);
      clearInterval(timer);
    };
  }, [checking]);

  return (
    <Button
      type="button"
      size="lg"
      className="mt-7 w-full sm:w-auto"
      disabled={checking}
      onClick={() => void attemptRecovery()}
    >
      {checking ? "Memeriksa koneksi..." : "Coba lagi"}
    </Button>
  );
}
