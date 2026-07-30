import Link from "next/link";
import { Icon } from "@/components/ui";
import { MentorScoreEditor } from "./MentorScoreEditor";

/** VOK-H5-E2 §2 mentor/nilai/[placementId]/page.tsx — form skor aspek industri (Teknis+Kehadiran). */
export default async function MentorNilaiDetailPage({
  params,
}: {
  params: Promise<{ placementId: string }>;
}) {
  const { placementId } = await params;

  return (
    <div className="flex flex-col gap-4">
      <div>
        <Link
          href="/mentor/nilai"
          className="inline-flex min-h-[var(--tap-min)] items-center gap-1.5 whitespace-nowrap rounded-[var(--radius-md)] text-sm text-primary outline-none hover:underline focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
        >
          <Icon name="arrow-left" size={16} />
          Kembali ke daftar
        </Link>
        <h1 className="mt-1 text-lg font-semibold text-ink">Penilaian Siswa</h1>
        <p className="text-sm text-ink-muted">Nilai aspek teknis & kehadiran selama PKL.</p>
      </div>

      <MentorScoreEditor placementId={placementId} />
    </div>
  );
}
