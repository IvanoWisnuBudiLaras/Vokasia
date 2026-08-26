import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import { PeriodStatusNum, type CompanyDto, type Paged, type PeriodSummary, type RubricDto } from "@/lib/apiTypes";
import { RubricTemplateWorkspace } from "./RubricTemplateWorkspace";

export const dynamic = "force-dynamic";

export default async function RubricPage() {
  const session = await getSession();
  if (session?.role !== "TenantAdmin") return <ErrorState message="Hanya admin sekolah yang dapat mengatur template penilaian." />;

  let rubric: RubricDto | null = null;
  let rubrics: RubricDto[] = [];
  let companies: CompanyDto[] = [];
  let periodLabel = "";
  let loadError = false;
  try {
    [rubrics, companies] = await Promise.all([
      fetcher<RubricDto[]>("/rubrics"),
      fetcher<CompanyDto[]>("/companies"),
    ]);
    const periods = await fetcher<Paged<PeriodSummary>>("/periods?pageSize=50");
    const period = periods.items.find((item) => Number(item.status) === PeriodStatusNum.Assessment) ?? periods.items[0];
    if (period) {
      periodLabel = period.name;
      try {
        rubric = await fetcher<RubricDto>(`/periods/${period.id}/rubric`);
      } catch {
        rubric = rubrics.find((item) => item.isDefault) ?? null;
      }
    }
  } catch (err) {
    console.error("[rubrik] gagal memuat template:", err);
    loadError = true;
  }

  return <div className="flex flex-col gap-6"><div><h1 className="text-3xl font-extrabold tracking-tight text-ink">Template penilaian</h1><p className="mt-1 text-base text-ink-muted">Atur kriteria guru dan mentor dengan bobot yang transparan.</p></div>{loadError ? <ErrorState message="Template penilaian belum bisa dimuat." /> : <RubricTemplateWorkspace initialRubric={rubric} rubrics={rubrics} companies={companies} periodLabel={periodLabel} />}</div>;
}
