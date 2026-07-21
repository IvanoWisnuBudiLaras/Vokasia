import { EmptyState } from "@/components/ui";
import { getSession } from "@/lib/session";

export const dynamic = "force-dynamic";

/**
 * VOK-H2-E2: sapaan nama via session. Daftar siswa bimbingan BELUM ditampilkan: backend belum
 * punya endpoint "placement milik mentor ini" (tak ada filter mentorUserId di ListPlacements) —
 * gap nyata, dicatat DECISIONS.md D16, di luar scope H2-E2. ApprovalList+SelectAllBar tetap
 * placeholder H3-E2.
 */
export default async function MentorHomePage() {
  const session = await getSession();

  return (
    <EmptyState
      icon="📋"
      title={session ? `Halo, ${session.name}` : "Belum ada jurnal untuk di-approve"}
      description="Jurnal yang menunggu persetujuanmu akan muncul di sini setelah endpoint terkait tersedia."
    />
  );
}
