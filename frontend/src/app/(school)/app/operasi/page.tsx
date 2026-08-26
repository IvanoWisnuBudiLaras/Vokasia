import { fetcher } from "@/lib/fetcher";
import type { CompanyDto, MajorOptionDto, Paged, PeriodSummary, PlacementDto, SchoolUserDto, StudentDto, TenantMentorSummaryDto } from "@/lib/apiTypes";
import { OperationsWorkspace } from "./OperationsWorkspace";

export const dynamic = "force-dynamic";

export default async function OperationsPage({ searchParams }: { searchParams: Promise<{ periodId?: string }> }) {
  const params = await searchParams;
  const [periods, students, staff, companies, majors] = await Promise.all([
    fetcher<Paged<PeriodSummary>>("/periods?pageSize=50"),
    fetcher<Paged<StudentDto>>("/students?pageSize=1000"),
    fetcher<Paged<SchoolUserDto>>("/school-users?pageSize=200"),
    fetcher<CompanyDto[]>("/companies"),
    fetcher<MajorOptionDto[]>("/students/majors"),
  ]);
  const periodId = params.periodId ?? periods.items[0]?.id;
  const [placements, mentors] = periodId ? await Promise.all([
    fetcher<Paged<PlacementDto>>(`/placements?periodId=${periodId}&pageSize=1000`),
    fetcher<TenantMentorSummaryDto[]>("/tenant-operations/mentors"),
  ]) : [{ items: [], page: 1, pageSize: 1000, totalCount: 0 } as Paged<PlacementDto>, [] as TenantMentorSummaryDto[]];
  return <OperationsWorkspace periods={periods.items} periodId={periodId ?? ""} students={students.items} staff={staff.items} companies={companies} majors={majors} placements={placements.items} mentors={mentors} />;
}
