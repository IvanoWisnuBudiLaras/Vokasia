import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { MentorAssessmentPlacementDto } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

/**
 * VOK-H5-E2 §2 mentor/nilai/page.tsx — daftar siswa fase Assessment milik mentor ini, via
 * `GET /api/mentors/assessment-queue` (endpoint baru, gap ditambal — lihat DECISIONS.md D34:
 * mentor lintas-tenant tak bisa pakai `GET /periods` biasa utk cari periode fase Assessment).
 */
export default async function MentorNilaiPage() {
  let placements: MentorAssessmentPlacementDto[] = [];
  let loadError = false;

  try {
    placements = await fetcher<MentorAssessmentPlacementDto[]>("/mentors/assessment-queue");
  } catch (err) {
    console.error("[mentor/nilai] gagal memuat antrean penilaian:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold text-ink">Penilaian Siswa</h1>
        <p className="text-sm text-ink-muted">Siswa bimbinganmu yang sedang di fase penilaian.</p>
      </div>

      {loadError && <ErrorState message="Daftar penilaian belum bisa dimuat. Coba muat ulang halaman." />}

      {!loadError && placements.length === 0 && (
        <EmptyState
          icon={<Icon name="file-pen-line" size={32} />}
          title="Belum ada siswa fase penilaian"
          description="Siswa bimbinganmu akan muncul di sini saat periode PKL-nya masuk fase penilaian."
        />
      )}

      {!loadError && placements.length > 0 && (
        <ul className="flex flex-col gap-2">
          {placements.map((p) => (
            <li key={p.placementId}>
              <a
                href={`/mentor/nilai/${p.placementId}`}
                className="flex items-center justify-between gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm outline-none transition-[color,background-color,border-color] hover:bg-primary-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
              >
                <div>
                  <p className="font-medium text-ink">{p.studentName}</p>
                  <p className="text-xs text-ink-muted">{p.companyName} · {p.periodName}</p>
                </div>
                <Icon name="arrow-right" size={16} />
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
