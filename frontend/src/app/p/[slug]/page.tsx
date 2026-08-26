import Link from "next/link";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { publicFetcher } from "@/lib/publicFetcher";
import { publicPortfolioCacheTag } from "@/lib/publicPortfolioCache";
import { CertificateVerificationStatus, type PublicPortfolioDto } from "@/lib/apiTypes";
import { PublicEvidenceGallery } from "./PublicEvidenceGallery";
import { ShareButton } from "./ShareButton";
import { ShareFileButton } from "./ShareFileButton";
import { AtsCvExportButton } from "./AtsCvExportButton";

interface PageProps { params: Promise<{ slug: string }> }

async function loadPortfolio(slug: string) {
  const result = await publicFetcher<PublicPortfolioDto>(`/p/${encodeURIComponent(slug)}`, 300, [publicPortfolioCacheTag(slug)]);
  return result.status === 404 ? null : result.data;
}

function dateLabel(value: string) {
  return new Intl.DateTimeFormat("id-ID", { dateStyle: "medium" }).format(new Date(value));
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const portfolio = await loadPortfolio(slug);
  if (!portfolio) return { title: "Portofolio tidak ditemukan — Vokasia" };
  return { title: `${portfolio.studentName} — Portofolio PKL | Vokasia`, description: `${portfolio.studentName}, ${portfolio.majorName} dari ${portfolio.schoolName}.` };
}

export default async function PublicPortfolioPage({ params }: PageProps) {
  const { slug } = await params;
  const portfolio = await loadPortfolio(slug);
  if (!portfolio) notFound();

  const certificate = portfolio.certificate;
  const certificateRevoked = certificate?.status === CertificateVerificationStatus.Revoked;
  portfolio.periodLabel = portfolio.periodLabel.replace(/^PKL\s+/i, "");

  return (
    <main className="min-h-screen bg-surface-paper px-5 py-8 sm:px-10 sm:py-14">
      <article className="mx-auto flex w-full max-w-4xl flex-col gap-12">
        <nav className="flex items-center justify-between gap-4 border-b border-border pb-5">
          <Link href="/" className="text-sm font-semibold text-ink">Vokasia</Link>
          <div className="flex flex-wrap items-center justify-end gap-2"><AtsCvExportButton slug={slug} /><ShareFileButton url={`/p/${encodeURIComponent(slug)}/cv`} filename={`cv-${slug}.pdf`} label="Bagikan CV PDF" title={`CV ATS ${portfolio.studentName}`} /><ShareButton title={`Portofolio PKL ${portfolio.studentName}`} /></div>
        </nav>

        <header className="flex flex-col gap-4">
          <h1 className="max-w-3xl text-4xl font-semibold tracking-tight text-ink sm:text-5xl">{portfolio.studentName}</h1>
          <p className="text-lg leading-7 text-ink-muted">{portfolio.majorName} · {portfolio.schoolName}</p>
          <div className="flex flex-col gap-1 text-sm text-ink-muted sm:flex-row sm:gap-3"><span>PKL {portfolio.periodLabel}</span><span className="hidden sm:inline" aria-hidden="true">·</span><span>{portfolio.companyName}</span><span className="hidden sm:inline" aria-hidden="true">·</span><span>{portfolio.durationLabel}</span></div>
          {portfolio.description && <p className="max-w-2xl text-base leading-7 text-ink">{portfolio.description}</p>}
        </header>

        {portfolio.verifiedCompetencies.length > 0 && <section aria-labelledby="kompetensi" className="flex flex-col gap-4"><h2 id="kompetensi" className="border-b border-border pb-3 text-xl font-semibold text-ink">Kompetensi terverifikasi</h2><ul className="grid gap-2 text-base text-ink sm:grid-cols-2">{portfolio.verifiedCompetencies.map((competency) => <li key={competency} className="border-l-2 border-primary pl-3">{competency}</li>)}</ul></section>}

        <PublicEvidenceGallery evidence={portfolio.evidence} studentName={portfolio.studentName} />

        {certificate && <section aria-labelledby="sertifikat" className={`flex flex-col gap-4 rounded-[var(--radius-lg)] border p-5 sm:p-6 ${certificateRevoked ? "border-status-red bg-status-red-bg" : "border-status-green bg-status-green-bg"}`}>
          <div><h2 id="sertifikat" className="text-xl font-semibold text-ink">Sertifikat PKL</h2><p className="mt-1 text-sm text-ink-muted">Diterbitkan oleh {portfolio.schoolName} · {dateLabel(certificate.issuedAt)}</p></div>
          <dl className="grid gap-3 text-sm sm:grid-cols-2"><div><dt className="text-ink-muted">Nomor sertifikat</dt><dd className="mt-1 break-all font-mono font-medium text-ink">{certificate.certificateNumber}</dd></div><div><dt className="text-ink-muted">Status</dt><dd className={`mt-1 font-medium ${certificateRevoked ? "text-status-red" : "text-status-green"}`}>{certificateRevoked ? "Dicabut" : "Valid"}</dd></div></dl>
          {certificateRevoked && certificate.publicRevocationReason && <p className="text-sm leading-6 text-status-red">Alasan pencabutan: {certificate.publicRevocationReason}</p>}
          <div className="flex flex-wrap gap-2"><a href={`/api/verify/${encodeURIComponent(certificate.certificateNumber)}/certificate`} target="_blank" rel="noreferrer" className="inline-flex min-h-[var(--tap-min)] items-center rounded-[var(--radius-md)] bg-surface px-4 text-sm font-medium text-ink ring-1 ring-border focus-visible:outline-2 focus-visible:outline-focus">Lihat sertifikat</a><ShareFileButton url={`/api/verify/${encodeURIComponent(certificate.certificateNumber)}/certificate?download=1`} filename={`sertifikat-${certificate.certificateNumber}.pdf`} label="Bagikan PDF" title={`Sertifikat PKL ${portfolio.studentName}`} /><Link href={`/verify/${encodeURIComponent(certificate.certificateNumber)}`} className="inline-flex min-h-[var(--tap-min)] items-center rounded-[var(--radius-md)] px-2 text-sm font-medium text-primary underline focus-visible:outline-2 focus-visible:outline-focus">Verifikasi</Link></div>
        </section>}

        <footer className="border-t border-border pt-5 text-sm text-ink-muted">Diverifikasi oleh Vokasia · <Link href={certificate ? `/verify/${encodeURIComponent(certificate.certificateNumber)}` : "/"} className="text-primary underline">Lihat verifikasi</Link></footer>
      </article>
    </main>
  );
}
