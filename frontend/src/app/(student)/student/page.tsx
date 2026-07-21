import { EmptyState } from "@/components/ui";
import { getSession } from "@/lib/session";

export const dynamic = "force-dynamic";

/**
 * VOK-H2-E2: sapaan nama via session (BFF H2-E3) — wiring end-to-end nyata. Placement/nama DUDI
 * BELUM ditampilkan: backend belum punya endpoint "placement milik siswa sendiri" (ListPlacements
 * butuh periodId wajib & tak ada filter studentId — Vokasia.Api/Endpoints/CompaniesAndPlacements.cs).
 * Gap nyata, dicatat DECISIONS.md D16, sengaja TIDAK diselesaikan diam-diam di ticket FE ini
 * (di luar file list VOK-H2-E2). JournalForm+PhotoUploader+WeekStrip tetap placeholder H3-E2.
 */
export default async function StudentTodayPage() {
  const session = await getSession();

  return (
    <EmptyState
      icon="📓"
      title={session ? `Halo, ${session.name}` : "Belum ada jurnal hari ini"}
      description="Form isi jurnal akan tampil di sini setelah slot harianmu tersedia. Info penempatan DUDI menyusul setelah endpoint terkait tersedia."
    />
  );
}
