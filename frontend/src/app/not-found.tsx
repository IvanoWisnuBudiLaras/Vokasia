"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Icon } from "@/components/ui";

export default function NotFound() {
  const pathname = usePathname();
  const institutional = pathname.startsWith("/app") || pathname.startsWith("/sa");

  return (
    <main
      data-theme={institutional ? undefined : "sekolah"}
      className="flex flex-1 items-center justify-center bg-surface px-5 py-10"
    >
      <section className="w-full max-w-lg rounded-[var(--radius-lg)] border border-border bg-surface p-6 shadow-sm sm:p-8">
        <div className="flex items-center gap-3 border-b border-border pb-5">
          <Image src="/icon.svg" alt="" width={40} height={40} />
          <div>
            <p className="font-semibold text-ink">Vokasia</p>
            <p className="text-xs text-ink-muted">Ruang belajar PKL SMK</p>
          </div>
        </div>

        <p className="mt-7 font-mono text-sm font-semibold text-primary">404 · HALAMAN TIDAK DITEMUKAN</p>
        <h1 className="mt-2 min-w-0 [overflow-wrap:anywhere] text-2xl font-bold tracking-tight text-ink">Lembar ini tidak tersedia.</h1>
        <p className="mt-3 text-sm leading-6 text-ink-muted">
          Alamat mungkin berubah, salah ketik, atau halaman sudah tidak dapat dibuka. Kembali ke
          beranda untuk melanjutkan.
        </p>

        <div className="mt-7 flex flex-col gap-3 sm:flex-row">
          <Link
            href="/"
            className="inline-flex h-[var(--tap-min)] items-center justify-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] bg-primary px-5 text-sm font-medium text-primary-ink outline-none hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
          >
            <Icon name="home" size={16} />
            Ke beranda
          </Link>
          <Link
            href="/login"
            className="inline-flex h-[var(--tap-min)] items-center justify-center whitespace-nowrap rounded-[var(--radius-md)] border border-border bg-surface px-5 text-sm font-medium text-ink outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
          >
            Masuk ke Vokasia
          </Link>
        </div>
      </section>
    </main>
  );
}
