import { EmptyState } from "@/components/ui";

/**
 * Placeholder publik H1 — diisi nyata di H6-E2 (GetPublicPortfolio, SSG/PPR + cache,
 * target LCP <2.5dtk 3G — NFR-PERF-01). Server Component murni, tanpa JS berat.
 */
export default async function PublicPortfolioPage({
  params,
}: {
  params: Promise<{ slug: string }>;
}) {
  const { slug } = await params;
  return (
    <main className="mx-auto max-w-md p-6">
      <EmptyState
        icon="🎓"
        title={`Portofolio "${slug}" belum tersedia`}
        description="Halaman portofolio publik akan aktif setelah siswa mempublikasikannya (H6)."
      />
    </main>
  );
}
