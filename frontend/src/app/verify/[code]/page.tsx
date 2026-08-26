import Link from "next/link";
import { Icon } from "@/components/ui";
import { CertificateVerificationStatus, type VerifyCertificateDto } from "@/lib/apiTypes";
import { publicFetcher } from "@/lib/publicFetcher";

interface PageProps { params: Promise<{ code: string }> }

export const metadata = { title: "Hasil Verifikasi — Vokasia" };

function dateLabel(value: string) {
  return new Intl.DateTimeFormat("id-ID", { dateStyle: "long" }).format(new Date(value));
}

export default async function VerifyCertificatePage({ params }: PageProps) {
  const { code } = await params;
  const { status: responseStatus, data } = await publicFetcher<VerifyCertificateDto>(`/api/verify/${encodeURIComponent(code)}`, 0);
  const found = responseStatus === 200 && data !== null;
  const revoked = found && data.status === CertificateVerificationStatus.Revoked;
  const positive = found && !revoked;

  return (
    <main className="min-h-screen bg-surface-paper px-5 py-10 sm:px-8 sm:py-16">
      <div className="mx-auto w-full max-w-2xl">
        <Link href="/verify" className="mb-6 inline-flex min-h-[var(--tap-min)] items-center gap-2 rounded-[var(--radius-md)] px-2 text-sm font-medium text-ink-muted focus-visible:outline-2 focus-visible:outline-focus"><Icon name="arrow-left" size={16} /> Periksa kode lain</Link>

        <section className={`rounded-[var(--radius-lg)] border p-6 sm:p-8 ${positive ? "border-status-green bg-status-green-bg" : revoked ? "border-status-red bg-status-red-bg" : "border-border bg-surface"}`}>
          <div className="flex items-start gap-3"><span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-surface ${positive ? "text-status-green" : revoked ? "text-status-red" : "text-ink-muted"}`}><Icon name={positive ? "check" : revoked ? "x" : "file-text"} size={24} /></span><div><p className="text-sm text-ink-muted">Verifikasi sertifikat</p><h1 className="mt-1 text-2xl font-semibold text-ink">{positive ? "Sertifikat valid" : revoked ? "Sertifikat dicabut" : "Sertifikat tidak ditemukan"}</h1></div></div>

          {found && data ? <>
            <dl className="mt-7 grid gap-4 border-y border-border py-5 text-sm sm:grid-cols-2"><div><dt className="text-ink-muted">Nomor sertifikat</dt><dd className="mt-1 break-all font-mono font-medium text-ink">{data.certificateNumber}</dd></div><div><dt className="text-ink-muted">Tanggal terbit</dt><dd className="mt-1 font-medium text-ink">{dateLabel(data.issuedAt)}</dd></div><div><dt className="text-ink-muted">Nama</dt><dd className="mt-1 font-medium text-ink">{data.studentName}</dd></div><div><dt className="text-ink-muted">Sekolah</dt><dd className="mt-1 font-medium text-ink">{data.schoolName}</dd></div><div><dt className="text-ink-muted">Program keahlian</dt><dd className="mt-1 font-medium text-ink">{data.majorName}</dd></div><div><dt className="text-ink-muted">DUDI</dt><dd className="mt-1 font-medium text-ink">{data.companyName}</dd></div><div><dt className="text-ink-muted">Periode</dt><dd className="mt-1 font-medium text-ink">{data.periodLabel}</dd></div></dl>
            {revoked && data.publicRevocationReason && <p className="text-sm leading-6 text-status-red">Alasan pencabutan: {data.publicRevocationReason}</p>}
            <div className="mt-6 flex flex-wrap gap-2"><a href={`/api/verify/${encodeURIComponent(data.certificateNumber)}/certificate`} target="_blank" rel="noreferrer" className="inline-flex min-h-[var(--tap-min)] items-center rounded-[var(--radius-md)] bg-surface px-4 text-sm font-medium text-ink ring-1 ring-border focus-visible:outline-2 focus-visible:outline-focus">Lihat sertifikat</a><a href={`/api/verify/${encodeURIComponent(data.certificateNumber)}/certificate?download=1`} className="inline-flex min-h-[var(--tap-min)] items-center rounded-[var(--radius-md)] bg-primary px-4 text-sm font-medium text-white focus-visible:outline-2 focus-visible:outline-focus">Unduh PDF</a></div>
          </> : <div className="mt-7 border-y border-border py-5"><p className="text-base text-ink-muted">Tidak ada sertifikat yang cocok dengan kode berikut:</p><p className="mt-2 break-all font-mono text-sm font-medium text-ink">{code}</p><p className="mt-3 text-sm leading-6 text-ink-muted">Periksa kembali kode atau pindai ulang QR dari dokumen asli.</p></div>}
        </section>
        <p className="mt-6 text-center text-sm text-ink-muted">Diverifikasi oleh Vokasia</p>
      </div>
    </main>
  );
}
