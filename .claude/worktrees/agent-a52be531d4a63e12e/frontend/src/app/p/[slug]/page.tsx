import { notFound } from "next/navigation";
import type { Metadata } from "next";
import { publicFetcher } from "@/lib/publicFetcher";
import type { PublicPortfolioDto } from "@/lib/apiTypes";

interface PageProps {
  params: Promise<{ slug: string }>;
}

async function loadPortfolio(slug: string): Promise<PublicPortfolioDto | null> {
  const { status, data } = await publicFetcher<PublicPortfolioDto>(`/p/${encodeURIComponent(slug)}`);
  return status === 404 ? null : data;
}

export async function generateMetadata({ params }: PageProps): Promise<Metadata> {
  const { slug } = await params;
  const portfolio = await loadPortfolio(slug);
  if (!portfolio) {
    return { title: "Portofolio tidak ditemukan — Vokasia" };
  }

  const title = `${portfolio.studentName} — Portofolio PKL ${portfolio.majorName} | Vokasia`;
  const description = `${portfolio.studentName}, siswa ${portfolio.majorName} ${portfolio.schoolName}, PKL di ${portfolio.companyName}.`;
  return {
    title,
    description,
    openGraph: { title, description, type: "profile" },
  };
}

/**
 * VOK-H6-E2 §2 p/[slug]/page.tsx — W6 publik: identitas (TANPA kontak/NISN — struktural, backend
 * PublicPortfolioDto tak pernah punya field itu, lihat apiTypes.ts), kompetensi terverifikasi,
 * sampel foto (thumbnail, lazy `<img loading="lazy">` — bukan next/image: URL presigned MinIO
 * berdomain/berparam dinamis per-request, tak cocok dgn allowlist next/image), badge sertifikat.
 * Server Component MURNI — nol "use client", nol JS interaktif (AC: LCP <2.5dtk 3G).
 */
export default async function PublicPortfolioPage({ params }: PageProps) {
  const { slug } = await params;
  const portfolio = await loadPortfolio(slug);

  if (!portfolio) {
    notFound();
  }

  return (
    <main data-theme="sekolah" className="mx-auto flex max-w-md flex-col gap-4 bg-surface p-6">
      <header className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-ink">{portfolio.studentName}</h1>
        <p className="text-sm text-ink-muted">
          {portfolio.majorName} · {portfolio.schoolName} · {portfolio.year}
        </p>
      </header>

      <section className="rounded-[var(--radius-lg)] border border-border p-4">
        <p className="text-sm text-ink-muted">PKL di</p>
        <p className="text-base font-medium text-ink">{portfolio.companyName}</p>
        <p className="text-sm text-ink-muted">Durasi: {portfolio.durationLabel}</p>
      </section>

      {portfolio.verifiedCompetencies.length > 0 && (
        <section>
          <h2 className="mb-2 text-sm font-semibold text-ink">Kompetensi Terverifikasi</h2>
          <ul className="flex flex-wrap gap-2">
            {portfolio.verifiedCompetencies.map((c) => (
              <li key={c} className="rounded-full bg-primary-muted px-3 py-1 text-xs font-medium text-ink">
                {c}
              </li>
            ))}
          </ul>
        </section>
      )}

      {portfolio.sampleThumbnailUrls.length > 0 && (
        <section>
          <h2 className="mb-2 text-sm font-semibold text-ink">Sampel Karya</h2>
          <div className="grid grid-cols-3 gap-2">
            {portfolio.sampleThumbnailUrls.map((url, i) => (
              // eslint-disable-next-line @next/next/no-img-element
              <img key={url} src={url} alt={`Sampel karya ${i + 1}`} loading="lazy" className="aspect-square w-full rounded-[var(--radius-md)] object-cover" />
            ))}
          </div>
        </section>
      )}

      {portfolio.hasCertificate && (
        <div className="rounded-[var(--radius-md)] bg-status-green-bg px-3 py-2 text-center text-sm font-medium text-status-green">
          🏆 Sertifikat PKL Terbit
        </div>
      )}
    </main>
  );
}
