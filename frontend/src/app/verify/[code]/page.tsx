import Image from "next/image";
import Link from "next/link";
import { Icon } from "@/components/ui";
import { publicFetcher } from "@/lib/publicFetcher";
import type { VerifyCertificateDto } from "@/lib/apiTypes";

interface PageProps {
  params: Promise<{ code: string }>;
}

export const metadata = {
  title: "Hasil Verifikasi — Vokasia",
};

/**
 * Hasil verifikasi hanya menampilkan enam field publik dari kontrak backend. Kode tidak pernah
 * diperlakukan sebagai HTML dan dibungkus agar input panjang tidak merusak layar kecil.
 */
export default async function VerifyCertificatePage({ params }: PageProps) {
  const { code } = await params;
  const { status, data } = await publicFetcher<VerifyCertificateDto>(
    `/api/verify/${encodeURIComponent(code)}`,
    0,
  );
  const valid = status === 200 && data !== null;

  return (
    <main data-theme="sekolah" className="flex flex-1 items-center justify-center bg-surface px-5 py-10">
      <div className="w-full min-w-0 max-w-lg">
        <Link
          href="/verify"
          className="mb-5 inline-flex min-h-[var(--tap-min)] items-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] px-2 text-base font-medium text-ink-muted outline-none hover:text-primary focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
        >
          <Icon name="arrow-left" size={16} />
          Periksa kode lain
        </Link>

        <section
          className={`min-w-0 rounded-[var(--radius-lg)] border p-6 shadow-sm sm:p-8 ${
            valid
              ? "border-status-green bg-status-green-bg"
              : "border-status-red bg-status-red-bg"
          }`}
        >
          <div className="flex items-start gap-3">
            <span
              className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-surface ${
                valid ? "text-status-green" : "text-status-red"
              }`}
            >
              <Icon name={valid ? "check" : "x"} size={24} />
            </span>
            <div className="min-w-0">
              <p className="text-xs font-semibold tracking-[0.12em] text-ink-muted">HASIL PEMERIKSAAN</p>
              <h1 className={`mt-1 min-w-0 [overflow-wrap:anywhere] text-xl font-semibold ${valid ? "text-status-green" : "text-status-red"}`}>
                {valid ? "Sertifikat terverifikasi" : "Sertifikat tidak ditemukan"}
              </h1>
            </div>
          </div>

          {valid && data ? (
            <dl className="mt-6 divide-y divide-status-green border-y border-status-green text-base text-ink">
              {[
                ["Nama", data.studentName],
                ["Sekolah", data.schoolName],
                ["DUDI", data.companyName],
                ["Periode", data.periodLabel],
              ].map(([label, value]) => (
                <div key={label} className="grid min-w-0 grid-cols-1 gap-1 py-3 min-[24rem]:grid-cols-[minmax(0,0.8fr)_minmax(0,1.2fr)] min-[24rem]:gap-3">
                  <dt className="text-ink-muted">{label}</dt>
                  <dd className="min-w-0 [overflow-wrap:anywhere] font-medium min-[24rem]:text-right">{value}</dd>
                </div>
              ))}
            </dl>
          ) : (
            <div className="mt-6 min-w-0 border-y border-status-red py-4">
              <p className="text-base leading-6 text-ink-muted">
                Tidak ada sertifikat yang cocok dengan kode berikut:
              </p>
              <p className="mt-2 min-w-0 break-all font-mono text-base font-medium text-ink">{code}</p>
              <p className="mt-3 text-base leading-6 text-ink-muted">
                Periksa kembali kode atau pindai ulang QR dari dokumen asli.
              </p>
            </div>
          )}

          <Link
            href="/"
            className="mt-6 inline-flex h-[var(--tap-min)] w-full items-center justify-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] bg-surface px-5 text-base font-medium text-ink outline-none ring-1 ring-border hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
          >
            <Icon name="home" size={16} />
            Kembali ke beranda
          </Link>
        </section>

        <div className="mt-5 flex items-center justify-center gap-2 text-xs text-ink-muted">
          <Image src="/icon.svg" alt="" width={24} height={24} />
          Verifikasi publik Vokasia
        </div>
      </div>
    </main>
  );
}
