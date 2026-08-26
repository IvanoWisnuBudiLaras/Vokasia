import { EmptyState, ErrorState, StatusBadge } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { AssessmentDto, JournalDto, Paged } from "@/lib/apiTypes";
import { RubricAspectKind } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

async function loadAssessment(): Promise<{ assessment: AssessmentDto | null; error: boolean }> {
  try {
    const journals = await fetcher<Paged<JournalDto>>("/journals?pageSize=1");
    const placementId = journals.items[0]?.placementId;
    if (!placementId) return { assessment: null, error: false };
    return { assessment: await fetcher<AssessmentDto>(`/placements/${placementId}/assessment`), error: false };
  } catch (error) {
    console.error("[student/penilaian] gagal memuat penilaian:", error);
    return { assessment: null, error: true };
  }
}

export default async function StudentAssessmentPage() {
  const { assessment, error } = await loadAssessment();

  return (
    <div className="flex max-w-3xl flex-col gap-5">
      <div className="flex flex-col gap-1 border-b border-border pb-4">
        <h1 className="text-2xl font-bold tracking-tight text-ink">Penilaian</h1>
        <p className="text-sm leading-6 text-ink-muted">Lihat komponen nilai yang diisi mentor industri dan guru pembimbing.</p>
      </div>

      {error && <ErrorState message="Penilaian belum bisa dimuat. Coba muat ulang halaman." />}
      {!error && !assessment && <EmptyState title="Penilaian belum tersedia" description="Penilaian akan muncul setelah penempatan dan rubrik penilaian siap." />}
      {!error && assessment && (
        <>
          <section aria-labelledby="assessment-status-heading" className="border-y border-border py-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h2 id="assessment-status-heading" className="text-sm font-semibold text-ink">Status pengisian</h2>
                <p className="mt-1 text-sm text-ink-muted">{assessment.isFinal ? "Nilai sudah difinalisasi dan terkunci." : "Nilai masih dalam proses pengisian."}</p>
              </div>
              <StatusBadge status={assessment.isFinal ? "green" : "amber"} label={assessment.isFinal ? "Final" : "Draft"} />
            </div>
            {assessment.isFinal && assessment.finalScore !== null && <p className="mt-3 text-2xl font-bold tabular-nums text-ink">{assessment.finalScore.toFixed(2)}</p>}
          </section>

          <div className="flex flex-col gap-6">
            {[
              { actor: "Mentor industri", value: "mentorValue" as const, kinds: [{ kind: RubricAspectKind.Teknis, label: "Teknis" }, { kind: RubricAspectKind.Kehadiran, label: "Kehadiran" }] },
              { actor: "Guru pembimbing", value: "teacherValue" as const, kinds: [{ kind: RubricAspectKind.Softskill, label: "Softskill" }] },
            ].map((group) => {
              const aspects = group.kinds.flatMap(({ kind, label }) => assessment.aspects.filter((aspect) => aspect.kind === kind).map((aspect) => ({ aspect, label })));
              if (aspects.length === 0) return null;
              return (
                <section key={group.actor} aria-labelledby={`assessment-side-${group.actor}`}>
                  <h2 id={`assessment-side-${group.actor}`} className="mb-2 text-sm font-semibold text-ink">{group.actor}</h2>
                  <div className="flex flex-col gap-3">
                    {group.kinds.map(({ kind, label }) => {
                      const kindAspects = aspects.filter((item) => item.aspect.kind === kind);
                      if (kindAspects.length === 0) return null;
                      return (
                        <div key={label}>
                          <h3 className="mb-1 text-xs font-semibold text-ink-muted">{label}</h3>
                          <ul className="border-y border-border">
                            {kindAspects.map(({ aspect }) => {
                              const value = aspect[group.value];
                              const showAspectName = aspect.aspectName !== label;
                              return (
                                <li key={aspect.aspectId} className="flex min-h-[var(--tap-min)] items-center justify-between gap-4 border-b border-border py-3 last:border-b-0">
                                  <span className="text-sm text-ink">{showAspectName && aspect.aspectName}<span className="ml-2 text-xs text-ink-muted">Bobot {aspect.weight}%</span></span>
                                  <span className="shrink-0 text-sm font-semibold tabular-nums text-ink">{value === null ? "Belum diisi" : value}</span>
                                </li>
                              );
                            })}
                          </ul>
                        </div>
                      );
                    })}
                  </div>
                </section>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}
