import { EmptyState } from "@/components/ui";

/** Placeholder H1 — diisi ApprovalList+SelectAllBar nyata di H3-E2 (GetPendingApprovals). */
export default function MentorHomePage() {
  return (
    <EmptyState
      icon="📋"
      title="Belum ada jurnal untuk di-approve"
      description="Jurnal yang menunggu persetujuanmu akan muncul di sini."
    />
  );
}
