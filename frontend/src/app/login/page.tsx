import Image from "next/image";
import Link from "next/link";
import { redirect } from "next/navigation";
import { getSafeLocalReturnUrl } from "@/lib/localReturnUrl";
import { roleHome } from "@/lib/roleHome";
import { getSession } from "@/lib/session";
import { getVerifiedSession } from "@/lib/serverSession";
const ERROR_COPY: Record<string, string> = {
  access_required: "Masuk dulu untuk membuka halaman ini.",
  unauthenticated: "Sesi berakhir. Silakan masuk kembali.",
  access_denied: "Kamu tidak punya akses ke halaman itu.",
};

interface LoginPageProps {
  searchParams: Promise<{ error?: string; next?: string }>;
}

export const metadata = {
  title: "Masuk — Vokasia",
};

/**
 * Halaman masuk memulai flow BFF dengan navigasi GET. Kata sandi dan token tidak disimpan
 * di halaman atau storage browser.
 */
export default async function LoginPage({ searchParams }: LoginPageProps) {
  const { error, next } = await searchParams;

  // Halaman login selalu merender form masuk. Jika ada error, itu indikasi sesi usang.
  const safeNext = getSafeLocalReturnUrl(next);
  const nextParam = safeNext
    ? `?next=${encodeURIComponent(safeNext)}`
    : "";

  return (
    <main className="flex min-h-screen flex-col items-center justify-center bg-surface px-6 py-12 selection:bg-brand-soft selection:text-ink">
      {/* Soft background ambient light */}
      <div className="pointer-events-none fixed left-1/2 top-1/3 -z-10 h-96 w-96 -translate-x-1/2 -translate-y-1/2 rounded-full bg-brand-soft/50 blur-3xl" />

      <div className="w-full max-w-md rounded-2xl border border-border/50 bg-surface p-8 shadow-[0_8px_30px_rgb(2,132,199,0.06)] sm:p-10">
        <div className="mb-8 flex flex-col items-center text-center">
          <Link href="/" className="mb-6 flex items-center gap-3">
            <Image src="/icon.svg" alt="Vokasia" width={36} height={36} priority />
            <span className="text-xl font-bold tracking-tight text-ink">Vokasia</span>
          </Link>
          <h1 className="text-2xl font-bold tracking-tight text-ink">Masuk ke Ruang Kerja</h1>
          <p className="mt-2 text-sm text-ink-muted leading-relaxed">
            Gunakan akun siswa, mentor, atau staf sekolah yang telah terdaftar.
          </p>
        </div>

        {error && (
          <div
            role="alert"
            className="mb-6 rounded-lg border border-status-red/20 bg-status-red/10 p-3.5 text-sm text-status-red"
          >
            {ERROR_COPY[error] ?? "Terjadi gangguan saat masuk. Silakan coba lagi."}
          </div>
        )}

        <div className="flex flex-col gap-4">
          <a
            href={`/api/auth/login${nextParam}`}
            className="inline-flex h-12 w-full items-center justify-center rounded-lg bg-primary px-6 text-base font-semibold text-white shadow-[0_2px_4px_0_oklch(50.4%_0.162_243.3/0.25)] transition-all hover:bg-brand-strong hover:-translate-y-0.5 active:translate-y-0"
          >
            Masuk ke Vokasia
          </a>
          <Link
            href="/"
            className="inline-flex h-10 items-center justify-center text-sm font-medium text-ink-muted transition-colors hover:text-ink"
          >
            ← Kembali ke Beranda
          </Link>
        </div>

        <div className="mt-8 border-t border-border/40 pt-6 text-center text-xs text-ink-muted">
          <p>Butuh verifikasi sertifikat? <Link href="/verify" className="font-semibold text-brand-action hover:underline">Periksa di sini</Link></p>
        </div>
      </div>
    </main>
  );
}
