"use client";

import { useSyncExternalStore, type ReactNode } from "react";

const subscribeToLocation = () => () => undefined;

export function OfflineThemeShell({ children }: { children: ReactNode }) {
  const pathname = useSyncExternalStore(
    subscribeToLocation,
    () => window.location.pathname,
    () => "/offline"
  );
  const institutional = pathname.startsWith("/app") || pathname.startsWith("/sa");

  return (
    <main
      data-theme={institutional ? undefined : "sekolah"}
      className="flex flex-1 items-center justify-center bg-surface px-5 py-10"
    >
      {children}
    </main>
  );
}
