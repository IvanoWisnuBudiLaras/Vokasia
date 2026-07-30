"use client";

import { Button } from "@/components/ui";

export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="id">
      <body className="flex min-h-screen items-center justify-center bg-surface px-5 py-10 font-sans text-ink">
        <main className="w-full max-w-lg rounded-[var(--radius-lg)] border border-status-red/30 bg-status-red-bg p-6 text-center">
          <h1 className="min-w-0 [overflow-wrap:anywhere] text-xl font-semibold text-status-red">
            Vokasia belum bisa dibuka
          </h1>
          <p className="mt-2 text-sm leading-6 text-ink-muted">
            Muat ulang aplikasi. Jika gangguan berlanjut, hubungi pengelola sekolah.
          </p>
          <Button type="button" variant="secondary" size="lg" className="mt-6" onClick={reset}>
            Muat ulang
          </Button>
        </main>
      </body>
    </html>
  );
}
