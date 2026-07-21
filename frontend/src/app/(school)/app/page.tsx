import { EmptyState } from "@/components/ui";

/** Placeholder H1 — diisi KpiCards+ProblemStudentList nyata di H4-E2 (GetSchoolDashboard). */
export default function SchoolDashboardPage() {
  return (
    <EmptyState
      icon="🏫"
      title="Dashboard Sekolah"
      description="Ringkasan jurnal, approval, dan siswa bermasalah akan tampil di sini (H4)."
    />
  );
}
