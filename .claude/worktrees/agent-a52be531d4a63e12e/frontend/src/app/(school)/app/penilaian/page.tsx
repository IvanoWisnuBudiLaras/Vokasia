import { EmptyState, ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import { PeriodStatusNum, type AssessmentDto, type Paged, type PeriodSummary, type PlacementDto, type StudentDto } from "@/lib/apiTypes";
import { TeacherScoreEditor } from "./TeacherScoreEditor";

export const dynamic = "force-dynamic";

interface RosterItem {
  placementId: string;
  studentName: string;
  mentorDone: boolean;
  teacherDone: boolean;
  isFinal: boolean;
}

/**
 * VOK-H5-E2 §2 app/penilaian/page.tsx — sisi guru: daftar siswa bimbingan (teacherId=session.id,
 * pola sama `app/bimbingan/page.tsx`) di periode fase Assessment PALING BARU (`/periods?status=2`,
 * angka bukan string - lihat komentar besar apiTypes.ts), status pengisian (mentor ✓/✗, guru ✓/✗)
 * per siswa dari `GetAssessment` — SATU panggilan per placement (N+1 diterima sengaja, caseload
 * guru dlm praktik terbatas puluhan, bukan ribuan; `Promise.all` biar concurrent bukan sequential).
 */
export default async function PenilaianPage({
  searchParams,
}: {
  searchParams: Promise<{ placementId?: string }>;
}) {
  const session = await getSession();
  const params = await searchParams;

  let roster: RosterItem[] = [];
  let loadError = false;
  let noAssessmentPeriod = false;

  try {
    const assessmentPeriods = await fetcher<Paged<PeriodSummary>>(`/periods?status=${PeriodStatusNum.Assessment}&pageSize=50`);
    const periodId = assessmentPeriods.items[0]?.id;

    if (!periodId) {
      noAssessmentPeriod = true;
    } else if (session) {
      const [placementsRes, studentsRes] = await Promise.all([
        fetcher<Paged<PlacementDto>>(`/placements?periodId=${periodId}&teacherId=${session.id}&pageSize=200`),
        fetcher<Paged<StudentDto>>("/students?pageSize=1000"),
      ]);
      const studentById = new Map(studentsRes.items.map((s) => [s.id, s]));

      roster = await Promise.all(
        placementsRes.items.map(async (p) => {
          const assessment = await fetcher<AssessmentDto>(`/placements/${p.id}/assessment`);
          return {
            placementId: p.id,
            studentName: studentById.get(p.studentId)?.fullName ?? "Siswa",
            mentorDone: assessment.mentorDone,
            teacherDone: assessment.teacherDone,
            isFinal: assessment.isFinal,
          };
        })
      );
    }
  } catch (err) {
    console.error("[penilaian] gagal memuat daftar penilaian:", err);
    loadError = true;
  }

  const selectedId = params.placementId ?? roster[0]?.placementId;
  const selected = roster.find((r) => r.placementId === selectedId);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-2">
        <div>
          <h1 className="text-xl font-semibold text-ink">Penilaian</h1>
          <p className="text-sm text-ink-muted">Isi nilai aspek softskill siswa bimbinganmu.</p>
        </div>
        <a href="/app/penilaian/rekap" className="text-sm font-medium text-primary hover:underline">
          Lihat Rekap →
        </a>
      </div>

      {loadError && <ErrorState message="Daftar penilaian belum bisa dimuat." />}

      {!loadError && noAssessmentPeriod && (
        <EmptyState icon="📝" title="Belum ada periode fase penilaian" description="Penilaian akan aktif saat periode PKL masuk fase Assessment." />
      )}

      {!loadError && !noAssessmentPeriod && roster.length === 0 && (
        <EmptyState icon="🎓" title="Belum ada siswa bimbingan" description="Kamu belum di-assign sebagai guru pembimbing di periode fase penilaian ini." />
      )}

      {roster.length > 0 && (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <ul className="flex flex-col gap-2 lg:col-span-1">
            {roster.map((item) => (
              <li key={item.placementId}>
                <a
                  href={`/app/penilaian?placementId=${item.placementId}`}
                  className={
                    "flex items-center justify-between gap-2 rounded-[var(--radius-md)] border p-3 text-sm outline-none transition-colors " +
                    "focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 " +
                    (item.placementId === selectedId
                      ? "border-primary bg-primary-muted"
                      : "border-border bg-surface hover:bg-surface-muted")
                  }
                >
                  <span className="font-medium text-ink">{item.studentName}</span>
                  <span className="flex gap-1 text-xs" title="Mentor / Guru">
                    <span className={item.mentorDone ? "text-status-green" : "text-ink-muted"}>M{item.mentorDone ? "✓" : "✗"}</span>
                    <span className={item.teacherDone ? "text-status-green" : "text-ink-muted"}>G{item.teacherDone ? "✓" : "✗"}</span>
                    {item.isFinal && <span className="text-primary">Final</span>}
                  </span>
                </a>
              </li>
            ))}
          </ul>

          <div className="lg:col-span-2">
            {selected ? (
              <>
                <h2 className="mb-2 text-sm font-semibold text-ink">Nilai — {selected.studentName}</h2>
                <TeacherScoreEditor placementId={selected.placementId} />
              </>
            ) : (
              <EmptyState title="Pilih siswa" description="Pilih salah satu siswa di daftar untuk mengisi nilai." />
            )}
          </div>
        </div>
      )}
    </div>
  );
}
