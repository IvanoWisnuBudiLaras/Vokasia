import { fetcher } from "@/lib/fetcher";
import type { Paged, PlacementDto, StudentDto } from "@/lib/apiTypes";
import { JournalReviewList } from "../JournalReviewList";

export const dynamic = "force-dynamic";

export default async function PlacementReviewPage({ params }: { params: Promise<{ placementId: string }> }) {
  const { placementId } = await params;
  const placement = await fetcher<PlacementDto>(`/placements/${placementId}`);
  const students = await fetcher<Paged<StudentDto>>("/students?pageSize=1000");
  const student = students.items.find((item) => item.id === placement.studentId);
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold text-ink">Jurnal siswa</h1>
      <p className="text-sm text-ink-muted">Tinjau jurnal, beri komentar, lalu lanjutkan ke kunjungan atau penilaian.</p>
      <JournalReviewList placementId={placementId} studentId={student?.id} studentName={student?.fullName} />
    </div>
  );
}
