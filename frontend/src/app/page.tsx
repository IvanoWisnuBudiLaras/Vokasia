import Image from "next/image";
import Link from "next/link";
import { redirect } from "next/navigation";
import { getSession } from "@/lib/session";
import { roleHome } from "@/lib/roleHome";

/** Landing publik Clean Coastal V2.1 */
export default async function LandingPage() {
  const session = await getSession();
  if (session) {
    redirect(roleHome(session.role));
  }
  return (
    <main className="flex min-h-screen flex-col bg-surface text-ink selection:bg-brand-soft selection:text-ink">
      {/* Navigation */}
      <header className="mx-auto flex w-full max-w-6xl items-center justify-between px-6 py-6 sm:px-10">
        <div className="flex items-center gap-3">
          <Image src="/icon.svg" alt="Vokasia" width={32} height={32} priority />
          <span className="text-xl font-bold tracking-tight text-ink">Vokasia</span>
        </div>
        <div className="flex items-center gap-4 sm:gap-6">
          <Link
            href="/verify"
            className="text-sm font-medium text-ink-muted hover:text-ink transition-colors hidden sm:inline-block"
          >
            Verifikasi Sertifikat
          </Link>
          <Link
            href="/login"
            className="inline-flex h-10 items-center justify-center rounded-lg bg-primary px-5 text-sm font-semibold text-white shadow-sm transition-all hover:bg-brand-strong hover:-translate-y-0.5 active:translate-y-0"
          >
            Masuk →
          </Link>
        </div>
      </header>

      {/* 1. Hero Section */}
      <section className="relative mx-auto flex w-full max-w-4xl flex-col items-center justify-center px-6 pb-16 pt-10 text-center sm:px-10 sm:pb-24 sm:pt-16">
        {/* Restrained coastal visual motif (subtle wave arc line) */}
        <div className="mb-6 flex justify-center text-brand-accent/40" aria-hidden="true">
          <svg width="48" height="12" viewBox="0 0 48 12" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M2 6C10 1 14 11 22 6C30 1 34 11 42 6C44 4.5 46 5 46 6" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
          </svg>
        </div>

        <h1 className="max-w-3xl text-2xl font-bold tracking-tight text-ink sm:text-4xl lg:text-[42px] leading-tight">
          PKL yang lebih terarah, terbukti, dan mudah dipantau.
        </h1>
        <p className="mt-5 max-w-2xl text-base sm:text-lg leading-relaxed text-ink-muted">
          Kelola penempatan, jurnal, bimbingan, penilaian, dan sertifikat dalam satu tempat.
        </p>

        <div className="mt-8 flex flex-col items-center justify-center gap-4 sm:flex-row sm:gap-4 w-full sm:w-auto">
          <Link
            href="/login"
            className="inline-flex h-11 w-full sm:w-auto items-center justify-center rounded-lg bg-primary px-6 text-sm font-semibold text-white shadow-[0_2px_4px_0_oklch(50.4%_0.162_243.3/0.25)] transition-all hover:bg-brand-strong hover:-translate-y-0.5 active:translate-y-0"
          >
            Masuk ke Vokasia
          </Link>
          <Link
            href="/verify"
            className="inline-flex h-11 w-full sm:w-auto items-center justify-center rounded-lg border border-border/60 bg-surface px-5 text-sm font-medium text-ink transition-colors hover:bg-surface-muted"
          >
            Verifikasi sertifikat →
          </Link>
        </div>
      </section>

      {/* 2. PKL Workflow — Open Process Rail (No Card Wall) */}
      <section className="border-t border-border/40 bg-surface-muted/30 py-14 sm:py-20">
        <div className="mx-auto max-w-5xl px-6 sm:px-10">
          <h2 className="text-center text-xl sm:text-2xl font-bold tracking-tight text-ink mb-12">
            5 Langkah Alur Praktik Kerja Lapangan
          </h2>

          {/* Desktop Horizontal Process Rail */}
          <div className="hidden lg:grid grid-cols-5 gap-6 relative">
            {[
              { step: "01", name: "Penempatan", desc: "Pemetaan DUDI, kuota slot, dan pembimbing." },
              { step: "02", name: "Jurnal", desc: "Pencatatan harian kegiatan & kompetensi siswa." },
              { step: "03", name: "Bimbingan", desc: "Catatan pengawasan & kunjungan berkala." },
              { step: "04", name: "Penilaian", desc: "Evaluasi rubrik dua sisi mentor dan guru." },
              { step: "05", name: "Sertifikat & Portofolio", desc: "Kredensial publik & dokumen resmi terverifikasi." },
            ].map((item, idx) => (
              <div key={item.step} className="flex flex-col relative pt-2">
                <div className="flex items-center gap-2 mb-3">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-brand-soft text-[11px] font-bold text-brand-action">
                    {idx + 1}
                  </span>
                  <span className="h-px flex-1 bg-border/60" />
                </div>
                <h3 className="font-bold text-ink text-sm">{item.name}</h3>
                <p className="mt-1 text-xs leading-relaxed text-ink-muted">{item.desc}</p>
              </div>
            ))}
          </div>

          {/* Mobile / Tablet Vertical Process Rail */}
          <div className="flex flex-col gap-6 lg:hidden max-w-md mx-auto">
            {[
              { step: "01", name: "Penempatan", desc: "Pemetaan DUDI, kuota slot, dan pembimbing." },
              { step: "02", name: "Jurnal", desc: "Pencatatan harian kegiatan & kompetensi siswa." },
              { step: "03", name: "Bimbingan", desc: "Catatan pengawasan & kunjungan berkala." },
              { step: "04", name: "Penilaian", desc: "Evaluasi rubrik dua sisi mentor dan guru." },
              { step: "05", name: "Sertifikat & Portofolio", desc: "Kredensial publik & dokumen resmi terverifikasi." },
            ].map((item, idx) => (
              <div key={item.step} className="flex items-start gap-4">
                <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-brand-soft text-xs font-bold text-brand-action mt-0.5">
                  {idx + 1}
                </span>
                <div>
                  <h3 className="font-bold text-ink text-sm">{item.name}</h3>
                  <p className="mt-1 text-xs leading-relaxed text-ink-muted">{item.desc}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* 3. Persona Summary — Open 2x2 Information Grid (No Card Wall) */}
      <section className="py-14 sm:py-20">
        <div className="mx-auto max-w-5xl px-6 sm:px-10">
          <h2 className="text-center text-xl sm:text-2xl font-bold tracking-tight text-ink mb-12">
            Dirancang untuk Setiap Peran PKL
          </h2>

          <div className="grid gap-x-12 gap-y-10 sm:grid-cols-2">
            {[
              { role: "Siswa", desc: "Akses mudah dari HP untuk mencatat kegiatan harian, mengunggah foto bukti belajar, dan membangun portofolio kompetensi." },
              { role: "Mentor Industri", desc: "Antrean persetujuan ringkas untuk meninjau, menyetujui, atau memberi catatan perbaikan pada jurnal siswa." },
              { role: "Guru Pembimbing", desc: "Pemantauan berbasis triase untuk memprioritaskan siswa yang membutuhkan intervensi atau kunjungan pembimbingan." },
              { role: "Sekolah & Admin", desc: "Pengelolaan operasional penempatan, periode aktif, pemantauan status kuota, dan rekapitulasi nilai akhir." },
            ].map((p) => (
              <div key={p.role} className="flex flex-col">
                <h3 className="text-base font-bold text-ink pb-2 border-b border-border/40">{p.role}</h3>
                <p className="mt-3 text-sm leading-relaxed text-ink-muted">{p.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* 4. Verified Outcome — Sea Mist Tonal Section (Inline Verified Statements) */}
      <section className="border-t border-border/40 bg-brand-soft/40 py-14 sm:py-20">
        <div className="mx-auto max-w-3xl px-6 text-center sm:px-10">
          <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-ink">
            Portofolio & Sertifikat Asli Terverifikasi
          </h2>
          <p className="mt-3 text-sm sm:text-base leading-relaxed text-ink-muted max-w-2xl mx-auto">
            Setiap pencapaian kompetensi dan kelulusan PKL dilengkapi dengan kode verifikasi unik serta QR terdaftar untuk menjamin keabsahan kredensial siswa.
          </p>

          <div className="mt-6 flex flex-wrap items-center justify-center gap-6 text-xs text-ink font-medium">
            <span className="inline-flex items-center gap-2">
              <svg className="h-4 w-4 text-status-green" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
              </svg>
              QR & Kode Verifikasi Resmi
            </span>
            <span className="inline-flex items-center gap-2">
              <svg className="h-4 w-4 text-brand-action" viewBox="0 0 20 20" fill="currentColor">
                <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
              </svg>
              Format Kredensial Portofolio Resmi
            </span>
          </div>
        </div>
      </section>

      {/* 5. Final CTA */}
      <section className="py-14 sm:py-20 text-center">
        <div className="mx-auto max-w-2xl px-6 sm:px-10">
          <h2 className="text-xl sm:text-2xl font-bold tracking-tight text-ink">
            Siap Memulai Praktik Kerja Lapangan?
          </h2>
          <p className="mt-2 text-sm text-ink-muted">
            Gunakan akun siswa, mentor, atau staf sekolah yang telah diberikan.
          </p>
          <div className="mt-6 flex justify-center">
            <Link
              href="/login"
              className="inline-flex h-11 items-center justify-center rounded-lg bg-primary px-7 text-sm font-semibold text-white shadow-[0_2px_4px_0_oklch(50.4%_0.162_243.3/0.25)] transition-all hover:bg-brand-strong hover:-translate-y-0.5 active:translate-y-0"
            >
              Masuk ke Vokasia →
            </Link>
          </div>
        </div>
      </section>
      {/* Footer */}
      <footer className="border-t border-border/40 py-8 text-center text-xs text-ink-muted">
        <p>© Vokasia — Ruang Belajar & Pembimbingan PKL SMK</p>
      </footer>
    </main>
  );
}
