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
        <a href="/mentor/nilai" className="text-sm text-primary hover:underline">
          ← Kembali ke Daftar
        </a>
        <h1 className="mt-1 text-lg font-semibold text-ink">Penilaian Siswa</h1>
        <p className="text-sm text-ink-muted">Nilai aspek teknis & kehadiran selama PKL.</p>
      </div>

      <MentorScoreEditor placementId={placementId} />
    </div>
  );
}
