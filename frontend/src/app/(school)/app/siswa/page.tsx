import { EmptyState, ErrorState, Icon } from "@/components/ui";
import type { CompanyDto, Paged, PeriodSummary, PlacementDto, StudentDto } from "@/lib/apiTypes";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import { PeriodSelector } from "../PeriodSelector";
import { TeacherRoster } from "./TeacherRoster";

export const dynamic = "force-dynamic";

export default async function TeacherRosterPage({ searchParams }: { searchParams: Promise<{ periodId?: string }> }) {
  const session = await getSession();
  const params = await searchParams;
  let periods: PeriodSummary[] = [];
  let students: StudentDto[] = [];
  let placements: PlacementDto[] = [];
  let companies: CompanyDto[] = [];
  let failed = false;

  try {
    const periodsPage = await fetcher<Paged<PeriodSummary>>("/periods?pageSize=50");
    periods = periodsPage.items;
    const periodId = params.periodId ?? periods[0]?.id;
    if (periodId) {
      const [studentPage, placementPage, companyList] = await Promise.all([
        fetcher<Paged<StudentDto>>("/students?pageSize=1000"),
        fetcher<Paged<PlacementDto>>(`/placements?periodId=${periodId}&teacherId=${session?.id ?? ""}&pageSize=1000`),
        fetcher<CompanyDto[]>("/companies"),
      ]);
      students = studentPage.items;
      placements = placementPage.items;
      companies = companyList;
    }
  } catch (error) {
    console.error("[teacher-roster] gagal memuat daftar siswa:", error);
    failed = true;
  }

  const selectedPeriodId = params.periodId ?? periods[0]?.id;
  if (session?.role !== "Teacher") {
    return <ErrorState message="Halaman ini hanya tersedia untuk guru pembimbing." />;
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-3xl font-extrabold tracking-tight text-ink">Siswa bimbingan</h1>
          <p className="mt-1 text-base text-ink-muted">Daftar ringan siswa pada periode terpilih. Urutan dimulai dari yang perlu perhatian.</p>
        </div>
        {selectedPeriodId && <PeriodSelector periods={periods} value={selectedPeriodId} />}
      </div>
      {failed && <ErrorState message="Daftar siswa belum bisa dimuat. Coba muat ulang halaman." />}
      {!failed && periods.length === 0 && <EmptyState icon={<Icon name="calendar-days" size={32} />} title="Belum ada periode" description="Periode PKL belum tersedia." />}
      {!failed && selectedPeriodId && <TeacherRoster students={students} placements={placements} companies={companies} periodId={selectedPeriodId} />}
    </div>
  );
}
