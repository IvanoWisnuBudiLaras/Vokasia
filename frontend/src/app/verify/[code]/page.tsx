import { EmptyState } from "@/components/ui";

/** Placeholder publik H1 — diisi nyata di H6-E2 (VerifyCertificate, tanpa data sensitif). */
export default async function VerifyCertificatePage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = await params;
  return (
    <main data-theme="sekolah" className="mx-auto max-w-md bg-surface p-6">
      <EmptyState
        icon="🔎"
        title={`Verifikasi sertifikat "${code}"`}
        description="Pengecekan keaslian sertifikat akan aktif di H6."
      />
    </main>
  );
}
