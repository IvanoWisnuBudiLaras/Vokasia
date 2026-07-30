"use client";

import { Button, Icon } from "@/components/ui";

interface RouteErrorStateProps {
  reset: () => void;
  theme?: "sekolah";
}

export function RouteErrorState({ reset, theme }: RouteErrorStateProps) {
  return (
    <main
      data-theme={theme}
      className="flex flex-1 items-center justify-center bg-surface px-5 py-10"
    >
      <section className="w-full max-w-lg rounded-[var(--radius-lg)] border border-status-red/30 bg-status-red-bg p-6 text-center">
        <Icon name="warning" size={32} className="mx-auto text-status-red" />
        <h1 className="mt-4 min-w-0 [overflow-wrap:anywhere] text-xl font-semibold text-status-red">
          Halaman belum bisa dimuat
        </h1>
        <p className="mt-2 text-sm leading-6 text-ink-muted">
          Periksa koneksi lalu coba lagi. Data yang sudah tersimpan tidak akan berubah.
        </p>
        <Button type="button" variant="secondary" size="lg" className="mt-6" onClick={reset}>
          Coba lagi
        </Button>
      </section>
    </main>
  );
}
