import Link from "next/link";
import { redirect } from "next/navigation";
import { Icon } from "@/components/ui";
import { VerifyForm } from "./VerifyForm";

interface VerifyPageProps {
  searchParams: Promise<{ code?: string }>;
}

export const metadata = {
  title: "Verifikasi Sertifikat — Vokasia",
};

export default async function VerifyPage({ searchParams }: VerifyPageProps) {
  const { code } = await searchParams;
  const normalizedCode = code?.trim();
  if (normalizedCode) redirect(`/verify/${encodeURIComponent(normalizedCode)}`);

  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-surface-paper px-6 py-12 selection:bg-brand-soft selection:text-ink">
      <section className="w-full max-w-md rounded-xl border border-border/40 bg-surface p-6 shadow-sm sm:p-8">
        <Link
          href="/"
          className="inline-flex min-h-[var(--tap-min)] items-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] px-2 text-sm font-medium text-ink-muted outline-none hover:text-primary focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
        >
          <Icon name="arrow-left" size={16} />
          Kembali ke beranda
        </Link>
        <p className="mt-6 text-xs font-semibold tracking-[0.12em] text-primary">VERIFIKASI PUBLIK</p>
        <h1 className="mt-2 min-w-0 [overflow-wrap:anywhere] text-2xl font-bold tracking-tight text-ink">Periksa sertifikat PKL</h1>
        <p className="mt-3 text-sm leading-6 text-ink-muted">
          Masukkan kode yang tercetak di sertifikat. Hasil hanya menampilkan data publik minimum.
        </p>

        <VerifyForm />
      </section>
    </main>
  );
}
