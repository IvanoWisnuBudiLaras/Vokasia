import { EmptyState, ErrorState, Icon } from "@/components/ui";
import Link from "next/link";
import { fetcher } from "@/lib/fetcher";
import type { StudentLearningRecordPlacementSummaryDto } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

const currentLabel = (stage: StudentLearningRecordPlacementSummaryDto["currentStage"], progressState: StudentLearningRecordPlacementSummaryDto["progressState"], legacyFinalOnly: boolean) =>
  legacyFinalOnly ? "Penilaian lama - hanya hasil akhir tersedia" : progressState === "CorrectionInProgress" ? "Penilaian sedang diperbaiki" : stage === "Final" ? "Penilaian Akhir selesai" : stage === "Middle" ? "Penilaian Tengah selesai" : "Menunggu Penilaian Tengah";

export default async function StudentPerkembanganPage() {
  let records: StudentLearningRecordPlacementSummaryDto[];
  try {
    records = await fetcher<StudentLearningRecordPlacementSummaryDto[]>("/students/me/learning-records");
  } catch (error) {
    console.error("[student/perkembangan] gagal memuat:", error);
    return <ErrorState message="Perkembangan belum bisa dimuat. Coba muat ulang halaman." />;
  }
  return (
    <div className="flex max-w-5xl flex-col gap-5">
      <header className="flex flex-col gap-1">
        <p className="text-sm font-medium text-primary">Learning Record</p>
        <h1 className="text-3xl font-extrabold tracking-tight text-ink">Perkembangan Pribadi</h1>
        <p className="text-base text-ink-muted">Hasil penilaian dari Mentor Industri atas kompetensi yang kamu latih selama PKL, terdiri dari Penilaian Tengah (Evaluasi Awal) dan Penilaian Akhir (Evaluasi Kelulusan).</p>
      </header>
      {records.length === 0 ? (
        <EmptyState icon={<Icon name="list-checks" size={32} />} title="Belum ada penempatan PKL" description="Perkembangan akan tampil setelah kamu memiliki penempatan resmi." />
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {records.map((record) => <li key={record.placementId}>
            <Link href={`/student/perkembangan/${record.placementId}`} className="block min-h-[var(--tap-min)] rounded-[var(--radius-lg)] border border-border/50 bg-surface p-5 outline-none transition-all hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus">
              <strong className="block text-lg text-ink">{record.companyName}</strong>
              <span className="mt-1 block text-sm text-ink-muted">{record.periodName}</span>
              <span className="mt-4 block text-sm font-medium text-primary">{currentLabel(record.currentStage, record.progressState, record.legacyFinalOnly)}</span>
            </Link>
          </li>)}
        </ul>
      )}
    </div>
  );
}
