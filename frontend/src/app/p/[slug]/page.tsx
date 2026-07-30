import Image from "next/image";
import Link from "next/link";
import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { Icon } from "@/components/ui";
import { publicFetcher } from "@/lib/publicFetcher";
import { publicPortfolioCacheTag } from "@/lib/publicPortfolioCache";
import type { PublicPortfolioDto } from "@/lib/apiTypes";

interface PageProps {
  params: Promise<{ slug: string }>;
}

async function loadPortfolio(slug: string): Promise<PublicPortfolioDto | null> {
  const { status, data } = await publicFetcher<PublicPortfolioDto>(
    `/p/${encodeURIComponent(slug)}`,
    300,
    [publicPortfolioCacheTag(slug)]
  );
  return status === 404 ? null : data;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const portfolio = await loadPortfolio(slug);
  if (!portfolio) return { title: "Portofolio tidak ditemukan — Vokasia" };

  const title = `${portfolio.studentName} — Portofolio PKL ${portfolio.majorName} | Vokasia`;
  const description = `${portfolio.studentName}, siswa ${portfolio.majorName} ${portfolio.schoolName}, PKL di ${portfolio.companyName}.`;
  return { title, description, openGraph: { title, description, type: "profile" } };
}

/**
 * Portofolio publik hanya memuat identitas aman, kompetensi terverifikasi, sampel karya,
 * dan status sertifikat. Kontak maupun NISN tidak menjadi bagian kontrak respons.
 */
export default async function PublicPortfolioPage({ params }: PageProps) {
  const { slug } = await params;
  const portfolio = await loadPortfolio(slug);
  if (!portfolio) notFound();

  return (
    <main data-theme="sekolah" className="flex flex-1 bg-surface px-5 py-8 sm:px-8 sm:py-12">
      <article className="mx-auto flex w-full min-w-0 max-w-2xl flex-col gap-7">
        <nav className="flex items-center justify-between gap-4 border-b border-border pb-4">
          <Link
            href="/"
            className="inline-flex min-h-[var(--tap-min)] items-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] px-2 text-sm font-medium text-ink-muted outline-none hover:text-primary focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
          >
            <Icon name="arrow-left" size={16} />
            Beranda
          </Link>
          <span className="flex items-center gap-2 text-xs text-ink-muted">
            <Image src="/icon.svg" alt="" width={24} height={24} />
            Portofolio Vokasia
          </span>
        </nav>

        <header className="min-w-0">
          <p className="text-xs font-semibold tracking-[0.12em] text-primary">PORTOFOLIO PKL</p>
          <h1 className="mt-2 min-w-0 [overflow-wrap:anywhere] text-3xl font-bold tracking-tight text-ink">{portfolio.studentName}</h1>
          <p className="mt-2 break-words text-sm leading-6 text-ink-muted">
            {portfolio.majorName} · {portfolio.schoolName} · {portfolio.year}
          </p>
        </header>

        <section className="grid gap-4 border-y border-border py-5 min-[30rem]:grid-cols-2">
          <div>
            <p className="text-xs font-semibold tracking-wide text-ink-muted">TEMPAT PKL</p>
            <p className="mt-1 break-words font-medium text-ink">{portfolio.companyName}</p>
          </div>
          <div>
            <p className="text-xs font-semibold tracking-wide text-ink-muted">DURASI</p>
            <p className="mt-1 break-words font-medium text-ink">{portfolio.durationLabel}</p>
          </div>
        </section>

        {portfolio.verifiedCompetencies.length > 0 && (
          <section aria-labelledby="kompetensi">
            <h2 id="kompetensi" className="text-base font-semibold text-ink">
              Kompetensi terverifikasi
            </h2>
            <ul className="mt-3 flex min-w-0 flex-wrap gap-2">
              {portfolio.verifiedCompetencies.map((competency) => (
                <li
                  key={competency}
                  className="max-w-full break-words rounded-full bg-primary-muted px-3 py-1.5 text-sm font-medium text-ink"
                >
                  {competency}
                </li>
              ))}
            </ul>
          </section>
        )}

        {portfolio.sampleThumbnailUrls.length > 0 && (
          <section aria-labelledby="sampel-karya">
            <h2 id="sampel-karya" className="text-base font-semibold text-ink">
              Sampel karya
            </h2>
            <div className="mt-3 grid min-w-0 grid-cols-[repeat(2,minmax(0,1fr))] gap-2 min-[25rem]:grid-cols-[repeat(3,minmax(0,1fr))]">
              {portfolio.sampleThumbnailUrls.map((url, index) => (
                // URL presigned MinIO dinamis tidak cocok untuk allowlist next/image.
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  key={url}
                  src={url}
                  alt={`Sampel karya ${index + 1}`}
                  width={640}
                  height={640}
                  loading={index === 0 ? "eager" : "lazy"}
                  fetchPriority={index === 0 ? "high" : "auto"}
                  className="aspect-square min-w-0 w-full rounded-[var(--radius-md)] border border-border object-cover"
                />
              ))}
            </div>
          </section>
        )}

        {portfolio.hasCertificate && (
          <div className="flex items-center justify-center gap-2 rounded-[var(--radius-md)] border border-status-green/30 bg-status-green-bg px-3 py-3 text-center text-sm font-medium text-status-green">
            <Icon name="award" size={20} />
            Sertifikat PKL telah terbit
          </div>
        )}
      </article>
    </main>
  );
}
