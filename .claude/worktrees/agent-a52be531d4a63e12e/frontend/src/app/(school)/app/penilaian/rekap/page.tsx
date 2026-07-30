import { EmptyState, ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { PeriodStatusNum, type Paged, type PeriodSummary, type RecapRowDto } from "@/lib/apiTypes";
import { RecapTable } from "./RecapTable";

export const dynamic = "force-dynamic";

/**
 * `PeriodSummary.status` DITULIS sbg union string literal di apiTypes.ts tp NILAI SEBENARNYA dari
 * backend adalah angka (PeriodStatus enum, tanpa JsonStringEnumConverter - lihat catatan besar di
 * puncak apiTypes.ts). `Number(p.status)` aman dipakai APA PUN bentuk runtime-nya (angka -> angka
 * sama, "2" -> 2) - helper kecil ini SATU-SATUNYA tempat kode baru ticket ini menyentuh field itu,
 * supaya tidak ikut mewarisi asumsi string yang salah.
 */
function periodStatusOf(p: PeriodSummary): number {
  return Number(p.status);
}

/**
 * VOK-H5-E2 §3 app/penilaian/rekap/page.tsx — rekap nilai (TenantAdmin/DeptHead+, policy
 * `GetGradeRecap`=TenantMember tp `RequestExport`=DeptHeadPlus & `FinalizeAssessment`=TenantAdminOnly
 * di backend - halaman ini boleh DIBUKA siapa saja tenant, tombol aksi akan 403 sendiri kalau role
 * tak cukup, sesuai pola RBAC-ditegakkan-di-backend seluruh app).
 *
 * Periode ditampilkan: SENGAJA otomatis (bukan selector manual, di luar literal AC ticket) -
 * prioritas periode fase Assessment (paling relevan utk aksi finalize/export) -> kalau tak ada,
 * periode Closed PALING BARU (rekap historis) -> fallback periode pertama apa pun.
 */
export default async function GradeRecapPage() {
  let rows: RecapRowDto[] = [];
  let loadError = false;
  let periodLabel = "";
  let noPeriod = false;
  let periodId: string | undefined;

  try {
    const periods = await fetcher<Paged<PeriodSummary>>("/periods?pageSize=50");
    const assessmentPeriod = periods.items.find((p) => periodStatusOf(p) === PeriodStatusNum.Assessment);
    const closedPeriods = periods.items.filter((p) => periodStatusOf(p) === PeriodStatusNum.Closed);
    const chosen = assessmentPeriod ?? closedPeriods[closedPeriods.length - 1] ?? periods.items[0];

    if (!chosen) {
      noPeriod = true;
    } else {
      periodId = chosen.id;
      periodLabel = chosen.name;
      rows = await fetcher<RecapRowDto[]>(`/periods/${periodId}/grade-recap`);
    }
  } catch (err) {
    console.error("[penilaian/rekap] gagal memuat rekap:", err);
    loadError = true;
  }

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-ink">Rekap Nilai</h1>
        {periodLabel && <p className="text-sm text-ink-muted">Periode: {periodLabel}</p>}
      </div>

      {loadError && <ErrorState message="Rekap nilai belum bisa dimuat." />}

      {!loadError && noPeriod && <EmptyState icon="📊" title="Belum ada periode" description="Buat periode PKL terlebih dahulu." />}

      {!loadError && periodId && rows.length === 0 && (
        <EmptyState icon="📊" title="Belum ada placement" description="Belum ada siswa placement di periode ini." />
      )}

      {!loadError && periodId && rows.length > 0 && <RecapTable periodId={periodId} initialRows={rows} />}
    </div>
  );
}
