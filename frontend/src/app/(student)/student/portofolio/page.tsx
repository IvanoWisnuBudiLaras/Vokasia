import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { PortfolioDto } from "@/lib/apiTypes";
import { PortfolioEditor } from "./PortfolioEditor";

export const dynamic = "force-dynamic";

export default async function StudentPortfolioPage() {
  let portfolio: PortfolioDto | null = null;
  let loadError = false;

  try {
    portfolio = await fetcher<PortfolioDto>("/portfolio");
  } catch (err) {
    console.error("[student/portofolio] gagal memuat:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-5 rounded-[var(--radius-lg)] border border-border/50 bg-surface p-6 shadow-sm">
      <div className="flex flex-col gap-1 border-b border-border pb-4">
        <h1 className="text-2xl font-bold tracking-tight text-ink">Portofolio</h1>
        <p className="text-sm text-ink-muted">Kelola dan publikasikan portofolio PKL kamu agar dapat dilihat publik dan industri.</p>
      </div>
      {loadError && <ErrorState message="Portofolio belum bisa dimuat." />}
      {!loadError && portfolio && <PortfolioEditor initialPortfolio={portfolio} />}
    </div>
  );
}
