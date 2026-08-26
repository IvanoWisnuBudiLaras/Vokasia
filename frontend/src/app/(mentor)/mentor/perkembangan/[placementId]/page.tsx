import { ErrorState } from "@/components/ui";
import Link from "next/link";
import { fetcher } from "@/lib/fetcher";
import type { LearningAssessmentDto } from "@/lib/apiTypes";
import { MentorLearningAssessment } from "../MentorLearningAssessment";

export const dynamic = "force-dynamic";

export default async function MentorPerkembanganDetailPage({ params }: { params: Promise<{ placementId: string }> }) {
  const { placementId } = await params;
  let assessments: [LearningAssessmentDto, LearningAssessmentDto];
  try {
    assessments = await Promise.all(["Middle", "Final"].map((stage) => fetcher<LearningAssessmentDto>(`/placements/${placementId}/learning-assessments/${stage}`))) as [LearningAssessmentDto, LearningAssessmentDto];
  } catch { return <ErrorState message="Detail Learning Record belum bisa dimuat. Coba muat ulang halaman." />; }
  return <div className="flex flex-col gap-5"><Link className="text-sm font-medium text-ink underline" href="/mentor/perkembangan">Kembali ke perkembangan siswa</Link><MentorLearningAssessment assessment={assessments[0]} /><MentorLearningAssessment assessment={assessments[1]} /></div>;
}
