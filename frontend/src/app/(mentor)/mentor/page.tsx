import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { PendingGroupDto } from "@/lib/apiTypes";
import { ApprovalList } from "./ApprovalList";

export const dynamic = "force-dynamic";

async function loadPending(): Promise<{ groups: PendingGroupDto[]; error: boolean }> {
  try {
    const groups = await fetcher<PendingGroupDto[]>("/journals/pending");
    return { groups, error: false };
  } catch (err) {
    console.error("[mentor] gagal memuat daftar approval:", err);
    return { groups: [], error: true };
  }
}

/** VOK-H3-E2 §2 mentor/page.tsx — daftar GetPendingApprovals grup per siswa, header hitung total. */
export default async function MentorHomePage() {
  const { groups, error } = await loadPending();
  const total = groups.reduce((sum, g) => sum + g.entries.length, 0);

  return (
    <div className="flex flex-col gap-6 max-w-4xl">
      <div className="flex flex-col gap-1">
        <h1 className="text-3xl font-extrabold tracking-tight text-ink">Antrean Persetujuan</h1>
        <p className="text-base text-ink-muted">{total === 0 ? "Semua jurnal sudah ditinjau." : `${total} jurnal menunggu tindakan Anda.`}</p>
      </div>

      <nav aria-label="Status antrean mentor" className="flex gap-1 overflow-x-auto border-b border-border">
        <span aria-current="page" className="flex min-h-[var(--tap-min)] items-center border-b-2 border-primary px-3 text-sm font-semibold text-ink">Menunggu <span className="ml-1 text-ink-muted">{total}</span></span>
        <button type="button" disabled className="min-h-[var(--tap-min)] cursor-not-allowed px-3 text-sm text-ink-muted opacity-60">Revisi</button>
        <button type="button" disabled className="min-h-[var(--tap-min)] cursor-not-allowed px-3 text-sm text-ink-muted opacity-60">Selesai</button>
      </nav>

      {error ? (
        <ErrorState message="Daftar jurnal belum bisa dimuat. Coba muat ulang halaman." />
      ) : (
        <ApprovalList initialGroups={groups} />
      )}
    </div>
  );
}
