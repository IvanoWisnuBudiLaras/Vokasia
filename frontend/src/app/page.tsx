import Image from "next/image";
import Link from "next/link";

const primaryLink =
  "inline-flex h-[var(--tap-min)] items-center justify-center whitespace-nowrap rounded-[var(--radius-md)] bg-primary px-6 text-base font-medium text-primary-ink shadow-sm outline-none transition-[color,background-color,border-color] duration-[var(--dur-fast)] hover:bg-primary/90 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px";

const secondaryLink =
  "inline-flex h-[var(--tap-min)] items-center justify-center whitespace-nowrap rounded-[var(--radius-md)] border border-border bg-surface px-5 text-base font-medium text-ink outline-none transition-[color,background-color,border-color] duration-[var(--dur-fast)] hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px";

/** Landing publik ringan: menjelaskan tugas produk tanpa aset berat atau klaim hasil palsu. */
export default function LandingPage() {
  return (
    <main data-theme="sekolah" className="flex flex-1 bg-surface">
      <section className="mx-auto flex w-full max-w-5xl flex-col justify-center px-5 pb-14 pt-10 sm:px-8 sm:pb-20 sm:pt-14 lg:pb-24 lg:pt-16">
        <header className="mb-10 flex items-center gap-3 border-b border-border pb-4">
          <Image src="/icon.svg" alt="" width={40} height={40} priority />
          <div>
            <p className="font-semibold tracking-tight text-ink">Vokasia</p>
            <p className="text-xs text-ink-muted">Ruang belajar PKL SMK</p>
          </div>
        </header>

        <div className="grid items-start gap-10 lg:grid-cols-[1.08fr_0.92fr] lg:gap-14">
          <div className="min-w-0 pt-1">
            <p className="text-sm font-semibold tracking-[0.14em] text-primary">CATAT · BIMBING · NILAI</p>
            <h1 className="mt-4 min-w-0 max-w-2xl [overflow-wrap:anywhere] text-4xl font-bold tracking-tight text-ink sm:text-5xl">
              Proses PKL yang tertib, dari jurnal sampai kompetensi.
            </h1>
            <p className="mt-5 max-w-xl text-base leading-7 text-ink-muted">
              Siswa mencatat kegiatan, mentor memberi umpan balik, dan sekolah memantau perkembangan
              dalam satu ruang kerja yang mudah dipahami.
            </p>

            <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:flex-wrap">
              <Link href="/login" className={primaryLink}>
                Masuk ke Vokasia
              </Link>
              <Link href="/verify" className={secondaryLink}>
                Verifikasi sertifikat
              </Link>
            </div>
            <p className="mt-4 max-w-xl text-sm leading-6 text-ink-muted">
              Gunakan akun siswa, mentor, atau staf yang diberikan sekolah maupun pengelola Vokasia.
            </p>
          </div>

          <section
            aria-labelledby="alur-pkl"
            className="overflow-hidden rounded-[var(--radius-lg)] border border-border bg-surface"
          >
            <div className="border-b border-border bg-surface-muted px-5 py-4">
              <h2 id="alur-pkl" className="text-lg font-semibold text-ink">
                Satu alur belajar yang jelas
              </h2>
            </div>
            <ol className="divide-y divide-border">
              {[
                ["01", "Catat", "Siswa merekam kegiatan harian dan bukti belajar."],
                ["02", "Bimbing", "Mentor menyetujui jurnal atau memberi catatan perbaikan."],
                ["03", "Nilai", "Sekolah meninjau progres dan hasil kompetensi PKL."],
              ].map(([number, title, description]) => (
                <li key={number} className="grid grid-cols-[2.75rem_1fr] gap-3 px-5 py-5">
                  <span className="text-sm font-semibold tabular-nums text-primary" aria-hidden="true">
                    {number}
                  </span>
                  <div>
                    <p className="font-semibold text-ink">{title}</p>
                    <p className="mt-1 text-sm leading-6 text-ink-muted">{description}</p>
                  </div>
                </li>
              ))}
            </ol>
          </section>
        </div>
      </section>
    </main>
  );
}
