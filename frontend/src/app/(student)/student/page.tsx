import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
import type { TodayJournalDto } from "@/lib/apiTypes";
import { TodayJournalCard } from "./TodayJournalCard";
import { PageHeading } from "@/components/PageHeading";

export const dynamic = "force-dynamic";

type LoadResult =
  | { kind: "ok"; data: TodayJournalDto }
  | { kind: "not-found" }
  | { kind: "error" };

async function loadToday(): Promise<LoadResult> {
  try {
    const data = await fetcher<TodayJournalDto>("/journals/today");
    return { kind: "ok", data };
  } catch (err) {
    const message = err instanceof Error ? err.message : "";
    if (message.includes("-> 404")) {
      return { kind: "not-found" };
    }
    console.error("[student/today] gagal memuat jurnal hari ini:", err);
    return { kind: "error" };
  }
}

function formatTanggal(): string {
  return new Date().toLocaleDateString("id-ID", { weekday: "long", day: "numeric", month: "long", year: "numeric" });
}

/**
 * VOK-H3-E2 §1 student/page.tsx (Server Component) — render TodayJournalDto (GetTodayJournal).
 *
 * [GAP header "perusahaan" - PRD W1 minta tanggal+perusahaan]: TodayJournalDto (backend H3-E1,
 * Endpoints/Dtos.cs) TIDAK membawa nama perusahaan/DUDI sama sekali (hanya slot/entry/competencies/
 * weekStatus/streak — dikonfirmasi baca langsung JournalEndpoints.cs GetTodayJournal). Satu-satunya
 * endpoint placement (`GET /placements`) butuh RbacPolicies.TenantMember (klaim tenant_id staf
 * sekolah) dan tak difilter per-siswa — TIDAK bisa dipanggil sesi siswa utk "placement milikku
 * sendiri" (persis gap yang SUDAH dicatat D16 sesi H2-E2, bukan temuan baru sesi ini). Header di
 * bawah sengaja HANYA tanggal, bukan mengarang nama perusahaan kosong/statis — konsisten dgn
 * WeekStrip.tsx (2 status jujur, bukan 3 status karangan). Perbaikan butuh field baru di backend,
 * di luar wilayah ticket ini (`frontend/` saja).
 */
export default async function StudentTodayPage() {
  const session = await getSession();
  const result = await loadToday();

  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        eyebrow="JURNAL HARI INI"
        title={session ? `Halo, ${session.name}` : "Hari ini"}
        description={formatTanggal()}
      />

      {/* Presensi belum masuk MVP; tidak ditampilkan sebagai data seolah sudah aktif. */}
      <div className="flex items-start gap-3 rounded-[var(--radius-md)] border border-dashed border-border bg-surface-muted p-3 text-sm text-ink-muted">
        <Icon name="calendar-days" size={20} className="mt-0.5 shrink-0" />
        <span>Presensi belum tersedia di aplikasi ini. Fokuskan dulu pada pengisian jurnal kegiatan harian.</span>
      </div>

      {result.kind === "error" && <ErrorState message="Jurnal hari ini belum bisa dimuat. Coba muat ulang halaman." />}

      {result.kind === "not-found" && (
        <EmptyState
          icon={<Icon name="notebook-pen" size={32} />}
          title="Belum ada slot jurnal untuk hari ini"
          description="Slot jurnal dibuat otomatis tiap pagi (05:00 WIB) untuk hari kerja. Kalau hari ini libur atau kamu belum punya penempatan aktif, slot memang belum tersedia."
        />
      )}

      {result.kind === "ok" && (
        <TodayJournalCard
          slot={result.data.slot}
          initialEntry={result.data.entry}
          competencies={result.data.competencies}
          initialWeekStatus={result.data.weekStatus}
          streak={result.data.streak}
          draftScope={session ? `${session.tenantId ?? "tanpa-tenant"}:${session.id}` : null}
        />
      )}
    </div>
  );
}
