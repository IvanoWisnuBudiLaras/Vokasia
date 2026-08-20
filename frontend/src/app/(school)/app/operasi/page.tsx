import { fetcher } from "@/lib/fetcher";
import type { MajorOptionDto, Paged, PeriodSummary, PlacementDto, SchoolUserDto, StudentDto, CompanyDto } from "@/lib/apiTypes";
import { OperationsWorkspace } from "./OperationsWorkspace";

export const dynamic = "force-dynamic";

export default async function OperationsPage() {
  const [periods, students, staff, companies, majors] = await Promise.all([
    fetcher<Paged<PeriodSummary>>("/periods?pageSize=50"),
    fetcher<Paged<StudentDto>>("/students?pageSize=200"),
    fetcher<Paged<SchoolUserDto>>("/school-users?pageSize=200"),
    fetcher<CompanyDto[]>("/companies"),
    fetcher<MajorOptionDto[]>("/students/majors"),
  ]);
  return <OperationsWorkspace periods={periods.items} students={students.items} staff={staff.items} companies={companies} majors={majors} />;
}
