import { ErrorState } from "@/components/ui";
import Link from "next/link";
import { fetcher } from "@/lib/fetcher";
import type { StudentLearningRecordPlacementDto } from "@/lib/apiTypes";
import { LearningRecordOverview } from "../LearningRecordOverview";

export const dynamic = "force-dynamic";

export default async function StudentPerkembanganDetailPage({ params }: { params: Promise<{ placementId: string }> }) {
  const { placementId } = await params;
  let record: StudentLearningRecordPlacementDto;
  try {
    record = await fetcher<StudentLearningRecordPlacementDto>(`/students/me/learning-records/${placementId}`);
  } catch (error) {
    console.error("[student/perkembangan/detail] gagal memuat:", error);
    return <ErrorState message="Detail perkembangan belum bisa dimuat. Coba kembali ke daftar perkembangan." />;
  }
  return <div className="flex flex-col gap-5 rounded-[var(--radius-lg)] border border-border/50 bg-surface p-6 shadow-sm"><Link href="/student/perkembangan" className="text-sm font-medium text-primary hover:underline">← Kembali ke perkembangan</Link><LearningRecordOverview record={record} /></div>;
}
