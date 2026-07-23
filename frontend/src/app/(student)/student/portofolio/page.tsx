import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { JournalEntryStatus, type JournalDto, type Paged, type PortfolioDto } from "@/lib/apiTypes";
import { PortfolioEditor } from "./PortfolioEditor";

export const dynamic = "force-dynamic";

/**
 * VOK-H6-E2 §3 student/portofolio/page.tsx — Server Component: ambil PortfolioDto milik siswa
 * (GetMyPortfolio) + daftar jurnal Approved milik siswa (sumber SamplePicker, reuse endpoint
 * GET /journals?status=1 yg SUDAH ADA sejak H3/H4 — bukan endpoint baru; lihat JournalEndpoints
 * .ListJournals, StudentSelf, otomatis terlingkup ke placement milik pemanggil sendiri).
 * pageSize=100 dianggap cukup utk 1 siswa selama masa PKL (~6 bln x ~20 hari kerja/bln < 150,
 * tapi Approved-only jauh lebih sedikit dari total submit — longgar; TIDAK ada UI pagination di
 * SamplePicker krn AC ticket cuma minta kurasi maks 6 sampel, bukan browsing arsip penuh).
 */
export default async function StudentPortfolioPage() {
  let portfolio: PortfolioDto | null = null;
  let approvedJournals: JournalDto[] = [];
  let loadError = false;

  try {
    [portfolio, approvedJournals] = await Promise.all([
      fetcher<PortfolioDto>("/portfolio"),
      fetcher<Paged<JournalDto>>(`/journals?status=${JournalEntryStatus.Approved}&pageSize=100`).then((p) => p.items),
    ]);
  } catch (err) {
    console.error("[student/portofolio] gagal memuat:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-ink">Portofolio</h1>

      {loadError && <ErrorState message="Portofolio belum bisa dimuat." />}

      {!loadError && portfolio && <PortfolioEditor initialPortfolio={portfolio} approvedJournals={approvedJournals} />}
    </div>
  );
}
