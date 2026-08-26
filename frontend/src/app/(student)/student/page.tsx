import { EmptyState, ErrorState } from "@/components/ui";
import { MaterialIcon } from "@/components/ui/MaterialIcon";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import type { StudentHomeDto, TodayJournalDto } from "@/lib/apiTypes";
import { TodayJournalCard } from "./TodayJournalCard";

export const dynamic = "force-dynamic";

type LoadResult =
  | { kind: "ok"; data: TodayJournalDto }
  | { kind: "not-found" }
  | { kind: "error" };

type HomeResult =
  | { kind: "ok"; data: StudentHomeDto }
  | { kind: "not-found" }
  | { kind: "error" };

async function loadToday(): Promise<LoadResult> {
  try {
    const data = await fetcher<TodayJournalDto>("/journals/today");
    return { kind: "ok", data };
  } catch (err) {
    const message = err instanceof Error ? err.message : "";
    if (message.includes("-> 404")) return { kind: "not-found" };
    console.error("[student/today] gagal memuat jurnal hari ini:", err);
    return { kind: "error" };
  }
}

async function loadHome(): Promise<HomeResult> {
  try {
    const data = await fetcher<StudentHomeDto>("/students/me/home");
    return { kind: "ok", data };
  } catch (err) {
    const message = err instanceof Error ? err.message : "";
    if (message.includes("-> 404")) return { kind: "not-found" };
    console.error("[student/home] gagal memuat ringkasan PKL:", err);
    return { kind: "error" };
  }
}

function formatTanggal(): string {
  return new Date().toLocaleDateString("id-ID", { weekday: "long", day: "numeric", month: "long", year: "numeric" });
}

const checklist = [
  ["Penempatan", "placementReady"],
  ["Jurnal aktif", "journalActive"],
  ["Penilaian", "assessmentStarted"],
  ["Sertifikat", "certificateIssued"],
] as const;

function statusLabel(status: number) {
  if (status === 1) return "PKL selesai";
  if (status === 2) return "PKL dihentikan";
  return "PKL aktif";
}

function formatRevisionDate(iso: string) {
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "short" });
}

function StudentHomeSummary({ data }: { data: StudentHomeDto }) {
  return (
    <>
      <section aria-labelledby="placement-summary-heading" className="border-y border-border py-4">
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <h2 id="placement-summary-heading" className="text-sm font-semibold text-ink">Ringkasan PKL</h2>
            <span className="text-sm font-semibold text-status-green">{statusLabel(data.status)}</span>
          </div>
          <dl className="grid grid-cols-2 gap-x-5 gap-y-3 text-sm sm:grid-cols-4">
            {[
              ["Perusahaan", data.companyName],
              ["Periode", data.periodName],
              ["Mentor industri", data.mentorName ?? "Belum ditentukan"],
              ["Guru pembimbing", data.teacherName ?? "Belum ditentukan"],
            ].map(([label, value]) => (
              <div key={label} className="min-w-0">
                <dt className="text-xs text-ink-muted">{label}</dt>
                <dd className="mt-1 truncate font-semibold text-ink" title={value}>{value}</dd>
              </div>
            ))}
          </dl>
        </div>
      </section>

      <section aria-labelledby="placement-checklist-heading" className="border-b border-border py-4">
        <h2 id="placement-checklist-heading" className="text-sm font-semibold text-ink">Tahapan PKL</h2>
        <ol className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
          {checklist.map(([label, key]) => (
            <li key={label} className="flex items-center gap-2 text-sm text-ink">
              <span className={`flex h-6 w-6 shrink-0 items-center justify-center rounded-full text-xs font-bold ${data[key] ? "bg-status-green-bg text-status-green" : "bg-surface-muted text-ink-muted"}`} aria-hidden="true">
                {data[key] ? "✓" : "–"}
              </span>
              <span>{label}</span>
            </li>
          ))}
        </ol>
      </section>

      {data.revisionItems.length > 0 && (
        <section aria-labelledby="attention-heading" className="rounded-lg bg-status-amber-bg p-4">
          <h2 id="attention-heading" className="text-sm font-semibold text-ink">Perlu perhatian</h2>
          <ul className="mt-2 flex flex-col gap-2 text-sm text-ink">
            {data.revisionItems.slice(0, 3).map((item) => (
              <li key={item.id} className="flex flex-wrap items-center justify-between gap-2">
                <span><span className="font-medium">Perlu revisi</span> · {formatRevisionDate(item.submittedAt)}</span>
                <a href="#jurnal-hari-ini" className="font-semibold text-primary underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-focus">Perbaiki jurnal</a>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  );
}

export default async function StudentTodayPage() {
  const session = await getSession();
  const [home, result] = await Promise.all([loadHome(), loadToday()]);

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-extrabold tracking-tight text-ink">Hari Ini</h1>
        <p className="text-base text-ink-muted">{formatTanggal()}</p>
      </div>

      {home.kind === "error" && <ErrorState message="Ringkasan PKL belum bisa dimuat. Coba muat ulang halaman." />}
      {home.kind === "ok" && <StudentHomeSummary data={home.data} />}

      {result.kind === "error" && <ErrorState message="Jurnal hari ini belum bisa dimuat. Coba muat ulang halaman." />}

      {result.kind === "not-found" && (
        <EmptyState
          icon={<MaterialIcon name="journal" decorative />}
          title="Belum ada slot jurnal untuk hari ini"
          description="Slot jurnal dibuat otomatis tiap pagi untuk hari kerja. Kalau hari ini libur atau kamu belum punya penempatan aktif, slot memang belum tersedia."
        />
      )}
      {result.kind === "ok" && (
        <TodayJournalCard
          slot={result.data.slot}
          initialEntry={result.data.entry}
          competencies={result.data.competencies}
          draftScope={session ? `${session.tenantId ?? "tanpa-tenant"}:${session.id}` : null}
        />
      )}
    </div>
  );
}
