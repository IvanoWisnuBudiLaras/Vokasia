import { PageHeading } from "@/components/PageHeading";
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
    <div className="flex flex-col gap-5">
      <PageHeading
        eyebrow="ANTRIAN TINJAUAN"
        title="Apa yang perlu saya tinjau?"
        description={total === 0 ? "Tidak ada jurnal yang menunggu persetujuan." : `${total} jurnal menunggu persetujuan Anda.`}
      />

      {error ? (
        <ErrorState message="Daftar jurnal belum bisa dimuat. Coba muat ulang halaman." />
      ) : (
        <ApprovalList initialGroups={groups} />
      )}
    </div>
  );
}
