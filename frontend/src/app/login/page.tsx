import Image from "next/image";
import Link from "next/link";
import { redirect } from "next/navigation";
import { Icon } from "@/components/ui";
import { getSafeLocalReturnUrl } from "@/lib/localReturnUrl";
import { roleHome } from "@/lib/roleHome";
import { getSession } from "@/lib/session";

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
  const session = await getSession();
  if (session) {
    const home = roleHome(session.role);
    if (home !== "/login") redirect(home);
  }

  const safeNext = getSafeLocalReturnUrl(next);
  const nextParam = safeNext
    ? `?next=${encodeURIComponent(safeNext)}`
    : "";

  return (
    <main data-theme="sekolah" className="flex flex-1 items-center justify-center bg-surface px-5 py-10">
      <div className="w-full max-w-md">
        <Link
          href="/"
          className="mb-5 inline-flex min-h-[var(--tap-min)] items-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] px-2 text-sm font-medium text-ink-muted outline-none hover:text-primary focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
        >
          <Icon name="arrow-left" size={16} />
          Kembali ke beranda
        </Link>

        <section className="rounded-[var(--radius-lg)] border border-border bg-surface p-6 shadow-sm sm:p-8">
          <div className="flex items-center gap-3">
            <Image src="/icon.svg" alt="" width={40} height={40} priority />
            <div>
              <p className="text-xs font-semibold tracking-[0.12em] text-primary">VOKASIA · PKL SMK</p>
              <p className="text-sm text-ink-muted">Ruang belajar dan bimbingan</p>
            </div>
          </div>

          <h1 className="mt-7 min-w-0 [overflow-wrap:anywhere] text-2xl font-bold tracking-tight text-ink">Masuk ke ruang PKL-mu</h1>
          <p className="mt-3 text-sm leading-6 text-ink-muted">
            Gunakan akun siswa, mentor, atau staf yang diberikan sekolah maupun pengelola Vokasia.
            Tidak ada pendaftaran akun mandiri di halaman ini.
          </p>

          {error && (
            <p
              role="alert"
              className="mt-5 rounded-[var(--radius-md)] border border-status-red/30 bg-status-red-bg p-3 text-sm text-status-red"
            >
              {ERROR_COPY[error] ?? "Terjadi gangguan saat masuk. Silakan coba lagi."}
            </p>
          )}

          <Link
            href={`/api/auth/login${nextParam}`}
            className="mt-7 inline-flex h-[var(--tap-min)] w-full items-center justify-center whitespace-nowrap rounded-[var(--radius-md)] bg-primary px-6 text-base font-medium text-primary-ink shadow-sm outline-none transition-[color,background-color,border-color] duration-[var(--dur-fast)] hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
          >
            Lanjut ke halaman masuk
          </Link>

          <p className="mt-4 text-xs leading-5 text-ink-muted">
            Kata sandi dipakai hanya untuk memeriksa akunmu saat masuk.
          </p>
        </section>
      </div>
    </main>
  );
}
