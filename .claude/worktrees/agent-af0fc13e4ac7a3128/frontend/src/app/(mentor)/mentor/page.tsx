import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import { getSession } from "@/lib/session";
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
  const session = await getSession();
  const { groups, error } = await loadPending();
  const total = groups.reduce((sum, g) => sum + g.entries.length, 0);

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-lg font-semibold text-ink">{session ? `Halo, ${session.name}` : "Approve Mingguan"}</h1>
        <p className="text-sm text-ink-muted">Jurnal menunggu approval ({total})</p>
      </div>

      {error ? (
        <ErrorState message="Daftar jurnal belum bisa dimuat. Coba muat ulang halaman." />
      ) : (
        <ApprovalList initialGroups={groups} />
      )}
    </div>
  );
}
