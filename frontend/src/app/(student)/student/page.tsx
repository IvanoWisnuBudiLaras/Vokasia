import { EmptyState } from "@/components/ui";

/** Placeholder H1 — diisi JournalForm+PhotoUploader+WeekStrip nyata di H3-E2 (GetTodayJournal). */
export default function StudentTodayPage() {
  return (
    <EmptyState
      icon="📓"
      title="Belum ada jurnal hari ini"
      description="Form isi jurnal akan tampil di sini setelah slot harianmu tersedia."
    />
  );
}
