import { PageHeading } from "@/components/PageHeading";
import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { Paged, SaStudentDto } from "@/lib/apiTypes";
import { StudentsTable } from "./StudentsTable";

export const dynamic = "force-dynamic";

export default async function SuperAdminStudentsPage() {
  let studentsData: Paged<SaStudentDto> | null = null;
  let loadError = false;

  try {
    studentsData = await fetcher<Paged<SaStudentDto>>("/sa/students?pageSize=200");
  } catch (err) {
    console.error("[sa] gagal memuat data siswa:", err);
    loadError = true;
  }

  if (loadError || !studentsData) {
    return <ErrorState message="Data siswa platform belum dapat dimuat." />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeading
        eyebrow="MANAJEMEN PLATFORM"
        title="Daftar Siswa Seluruh Sekolah"
        description="Pantau dan kelola data siswa PKL dari seluruh SMK tenant terdaftar di platform Vokasia."
      />

      <StudentsTable initialStudents={studentsData.items} totalCount={studentsData.totalCount} />
    </div>
  );
}
