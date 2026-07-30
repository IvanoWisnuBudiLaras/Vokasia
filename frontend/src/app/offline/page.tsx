import Image from "next/image";
import { Icon } from "@/components/ui";
import { OfflineRetryButton } from "./OfflineRetryButton";
import { OfflineThemeShell } from "./OfflineThemeShell";

export const metadata = {
  title: "Sedang offline — Vokasia",
};

export default function OfflinePage() {
  return (
    <OfflineThemeShell>
      <section className="w-full max-w-lg rounded-[var(--radius-lg)] border border-status-amber/30 bg-status-amber-bg p-6 sm:p-8">
        <div className="flex items-center gap-3">
          <Image src="/icon.svg" alt="" width={40} height={40} />
          <div>
            <p className="font-semibold text-ink">Vokasia</p>
            <p className="text-xs text-ink-muted">Ruang belajar PKL SMK</p>
          </div>
        </div>

        <Icon name="warning" size={32} className="mt-7 text-status-amber" />
        <h1 className="mt-3 min-w-0 [overflow-wrap:anywhere] text-2xl font-bold tracking-tight text-ink">
          Kamu sedang offline
        </h1>
        <p className="mt-3 text-sm leading-6 text-ink-muted">
          Sambungkan perangkat ke internet sebelum mengirim jurnal atau membuka data terbaru.
          Vokasia tidak menyimpan halaman akun maupun data siswa untuk dibuka secara offline.
        </p>

        <OfflineRetryButton />
      </section>
    </OfflineThemeShell>
  );
}
