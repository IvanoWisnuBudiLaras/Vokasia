import { EmptyState, ErrorState, Icon } from "@/components/ui";
import { RichTextContent } from "@/components/ui/RichTextContent";
import { fetcher } from "@/lib/fetcher";
import { richTextPlainText } from "@/lib/richText";
import type { JournalDto, Paged } from "@/lib/apiTypes";

export const dynamic = "force-dynamic";

async function loadJournals(): Promise<{ items: JournalDto[]; error: boolean }> {
  try {
    const result = await fetcher<Paged<JournalDto>>("/journals?pageSize=200");
    return { items: result.items, error: false };
  } catch (error) {
    console.error("[student/bimbingan] gagal memuat timeline jurnal:", error);
    return { items: [], error: true };
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("id-ID", { day: "numeric", month: "short", year: "numeric" });
}

function needsExpansion(text: string) {
  const plainText = richTextPlainText(text);
  return plainText.length > 180 || plainText.split(/\r?\n/).length > 3;
}

/** Timeline bimbingan siswa memakai journal + mentorNote yang benar-benar tersedia dari API StudentSelf. */
export default async function StudentGuidancePage() {
  const { items, error } = await loadJournals();

  return (
    <div className="flex max-w-3xl flex-col gap-5">
      <div className="flex flex-col gap-1 border-b border-border pb-4">
        <h1 className="text-2xl font-bold tracking-tight text-ink">Bimbingan</h1>
        <p className="text-sm leading-6 text-ink-muted">Catatan dari perjalanan jurnalmu, terbaru sampai yang paling lama.</p>
      </div>

      {error && <ErrorState message="Timeline bimbingan belum bisa dimuat. Coba muat ulang halaman." />}
      {!error && items.length === 0 && (
        <EmptyState icon={<Icon name="message-square-text" size={32} />} title="Belum ada catatan bimbingan" description="Catatan mentor akan muncul setelah kamu mengirim jurnal." />
      )}
      {!error && items.length > 0 && (
        <ol className="relative border-l border-border pl-5">
          {items.map((journal) => (
            <li key={journal.id} className="relative pb-6 last:pb-0">
              <span aria-hidden="true" className="absolute -left-[1.6rem] top-1 h-3 w-3 rounded-full border-2 border-surface bg-primary" />
              <p className="text-xs font-medium text-ink-muted">{formatDate(journal.submittedAt)}</p>
              {needsExpansion(journal.text) ? (
                <details className="mt-1 group">
                  <summary className="cursor-pointer list-none outline-none focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 [&::-webkit-details-marker]:hidden">
                    <RichTextContent value={journal.text} className="line-clamp-3 text-sm leading-6 text-ink" />
                    <span className="mt-1 inline-flex min-h-[var(--tap-min)] items-center text-xs font-semibold text-primary">Lihat selengkapnya</span>
                  </summary>
                  <div className="mt-2 flex flex-col gap-2 border-l-2 border-primary-muted pl-3 text-sm leading-6 text-ink">
                    <RichTextContent value={journal.text} className="flex flex-col gap-2" />
                    <p className={journal.mentorNote ? "text-status-red" : "text-ink-muted"}>
                      <strong>{journal.mentorNote ? "Catatan mentor:" : "Catatan bimbingan:"}</strong>{" "}
                      {journal.mentorNote ?? "Belum ada catatan mentor."}
                    </p>
                    <a href={`/student/history?journalId=${encodeURIComponent(journal.id)}`} className="inline-flex min-h-[var(--tap-min)] items-center text-sm font-semibold text-primary underline-offset-2 hover:underline focus-visible:outline-2 focus-visible:outline-focus">
                      Buka jurnal terkait
                    </a>
                  </div>
                </details>
              ) : (
                <div className="mt-1 flex flex-col gap-2 text-sm leading-6 text-ink">
                  <RichTextContent value={journal.text} className="flex flex-col gap-2" />
                  <p className={journal.mentorNote ? "text-status-red" : "text-ink-muted"}>
                    <strong>{journal.mentorNote ? "Catatan mentor:" : "Catatan bimbingan:"}</strong>{" "}
                    {journal.mentorNote ?? "Belum ada catatan mentor."}
                  </p>
                </div>
              )}
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
