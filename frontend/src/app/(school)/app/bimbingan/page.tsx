import { cn } from "@/lib/cn";
import { EmptyState, ErrorState, Icon, StatusBadge } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import {
  ragLabel,
  ragToBadgeStatus,
  RagStatus,
  type Paged,
  type PeriodSummary,
  type PlacementDto,
  type SchoolDashboardDto,
  type StudentDto,
} from "@/lib/apiTypes";
import { JournalReviewList } from "./JournalReviewList";

export const dynamic = "force-dynamic";

interface CaseloadItem {
  placementId: string;
  studentId: string;
  studentName: string;
  rag: number;
  reason: string;
}

/**
 * VOK-H4-E2 §2 app/bimbingan/page.tsx — khusus role Teacher (AC: "hanya siswa bimbingannya, scope
 * dari API — UI tidak memfilter sendiri"): `ListPlacements?teacherId=` (filter BARU, sengaja
 * dikirim tanpa cek role sisi FE — kalau TenantAdmin/DeptHead buka halaman ini, teacherId=id
 * mereka sendiri wajar kembalikan kosong krn mereka bukan guru manapun, BUKAN bug). RAG per siswa
 * DIAMBIL dari GetSchoolDashboard yang SUDAH ADA (bukan endpoint baru): siswa yang TIDAK muncul di
 * `flagged` = Green by definition (H4-E1: flagged = "Rag != Green") — dijelaskan komentar di bawah.
 * Nama siswa: `ListStudents` tak py filter "by ids" (gap dicatat, bukan endpoint baru lagi krn
 * marginal — 1 fetch tambahan pageSize besar diterima utk scope H4-E2 ini).
 */
export default async function BimbinganPage({
  searchParams,
}: {
  searchParams: Promise<{ placementId?: string }>;
}) {
  const session = await getSession();
  const params = await searchParams;

  let periods: PeriodSummary[] = [];
  let loadError = false;
  let caseload: CaseloadItem[] = [];

  try {
    const pagedPeriods = await fetcher<Paged<PeriodSummary>>("/periods?pageSize=50");
    periods = pagedPeriods.items;
    const periodId = periods[0]?.id;

    if (periodId && session) {
      const [placementsRes, studentsRes, dashboard] = await Promise.all([
        fetcher<Paged<PlacementDto>>(`/placements?periodId=${periodId}&teacherId=${session.id}&pageSize=200`),
        fetcher<Paged<StudentDto>>("/students?pageSize=1000"),
        fetcher<SchoolDashboardDto>(`/dashboard/school/${periodId}`),
      ]);

      const studentById = new Map(studentsRes.items.map((s) => [s.id, s]));
      // flagged (H4-E1) = "Rag != Green" by design (lihat GetSchoolDashboard) - siswa yang TIDAK
      // ada di sini otomatis Green, bukan diasumsikan/dikarang di sini.
      const flaggedByStudentId = new Map(dashboard.flagged.map((f) => [f.studentId, f]));

      caseload = placementsRes.items.map((p) => {
        const flaggedEntry = flaggedByStudentId.get(p.studentId);
        return {
          placementId: p.id,
          studentId: p.studentId,
          studentName: studentById.get(p.studentId)?.fullName ?? "Siswa",
          rag: flaggedEntry?.rag ?? RagStatus.Green,
          reason: flaggedEntry?.reason ?? "Jurnal terisi sesuai jadwal.",
        };
      });
    }
  } catch (err) {
    console.error("[bimbingan] gagal memuat siswa bimbingan:", err);
    loadError = true;
  }

  const selectedId = params.placementId ?? caseload[0]?.placementId;
  const selected = caseload.find((c) => c.placementId === selectedId);

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-ink">Bimbingan Saya</h1>
        <p className="text-sm text-ink-muted">Siswa yang di-assign kepadamu sebagai guru pembimbing.</p>
      </div>

      {loadError && <ErrorState message="Daftar siswa bimbingan belum bisa dimuat." />}

      {!loadError && caseload.length === 0 && (
        <EmptyState
          icon={<Icon name="graduation-cap" size={32} />}
          title="Belum ada siswa bimbingan"
          description="Kamu belum ditetapkan sebagai guru pembimbing di periode aktif."
        />
      )}

      {caseload.length > 0 && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <ul className="flex flex-col gap-2 lg:col-span-1">
            {caseload.map((item) => (
              <li key={item.placementId}>
                <a
                  href={`/app/bimbingan?placementId=${item.placementId}`}
                  className={cn(
                    "flex items-center justify-between gap-2 rounded-[var(--radius-md)] border p-3 text-sm outline-none transition-[color,background-color,border-color]",
                    "focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2",
                    item.placementId === selectedId
                      ? "border-primary bg-primary-muted"
                      : "border-border bg-surface hover:bg-surface-muted"
                  )}
                >
                  <span className="font-medium text-ink">{item.studentName}</span>
                  <StatusBadge
                    status={ragToBadgeStatus(item.rag)}
                    label={ragLabel(item.rag)}
                  />
                </a>
              </li>
            ))}
          </ul>

          <div className="lg:col-span-2">
            {selected ? (
              <>
                <div className="mb-2 flex items-center justify-between gap-2">
                  <h2 className="text-sm font-semibold text-ink">Jurnal — {selected.studentName}</h2>
                  <a
                    href={`/app/bimbingan/${selected.placementId}/kunjungan`}
                    className="inline-flex min-h-[var(--tap-min)] items-center gap-1.5 rounded-[var(--radius-md)] border border-border bg-surface-muted px-3 text-xs font-medium text-ink outline-none transition-[color,background-color,border-color] hover:bg-border/40 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
                  >
                    <Icon name="map-pin" size={16} /> Catat Kunjungan
                  </a>
                </div>
                <JournalReviewList placementId={selected.placementId} studentId={selected.studentId} studentName={selected.studentName} />
              </>
            ) : (
              <EmptyState
                title="Pilih siswa"
                description="Pilih salah satu siswa di daftar untuk melihat jurnalnya."
                action={
                  <a
                    href={`/app/bimbingan?placementId=${caseload[0].placementId}`}
                    className="inline-flex min-h-[var(--tap-min)] items-center rounded-[var(--radius-md)] border border-border px-3 text-sm font-medium text-primary outline-none hover:bg-primary-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:bg-primary-muted"
                  >
                    Pilih {caseload[0].studentName}
                  </a>
                }
              />
            )}
          </div>
        </div>
      )}
    </div>
  );
}
