import { EmptyState, ErrorState, Icon } from "@/components/ui";
import Link from "next/link";
import { fetcher } from "@/lib/fetcher";
import type { MentorAssessmentPlacementDto } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

export default async function MentorPerkembanganPage() {
  let placements: MentorAssessmentPlacementDto[];
  try {
    placements = await fetcher<MentorAssessmentPlacementDto[]>("/mentors/assessment-queue");
  } catch { return <ErrorState message="Daftar perkembangan belum bisa dimuat. Coba muat ulang halaman." />; }
  return <div className="mx-auto flex max-w-5xl flex-col gap-4"><div><h1 className="text-lg font-semibold text-ink">Perkembangan Siswa</h1><p className="text-sm text-ink-muted">Isi Learning Record Tengah atau Akhir untuk siswa bimbinganmu.</p></div>{placements.length === 0 ? <EmptyState icon={<Icon name="file-pen-line" size={32} />} title="Belum ada penilaian" description="Siswa akan muncul saat periode penilaian tersedia." /> : <ul className="grid gap-3 sm:grid-cols-2">{placements.map((item) => <li key={item.placementId}><Link className="block rounded-[var(--radius-lg)] border border-border/50 bg-surface p-4 outline-none hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus" href={`/mentor/perkembangan/${item.placementId}`}><strong className="block text-ink">{item.studentName}</strong><span className="mt-1 block text-sm text-ink-muted">{item.companyName}</span></Link></li>)}</ul>}</div>;
}
