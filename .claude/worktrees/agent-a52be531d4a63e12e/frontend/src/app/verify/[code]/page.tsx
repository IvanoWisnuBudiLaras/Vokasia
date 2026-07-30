import { publicFetcher } from "@/lib/publicFetcher";
import type { VerifyCertificateDto } from "@/lib/apiTypes";

interface PageProps {
  params: Promise<{ code: string }>;
}

/**
 * VOK-H6-E2 §2 verify/[code]/page.tsx — hasil VerifyCertificate: ✔ terverifikasi (nama, sekolah,
 * DUDI, periode) / ✖ tidak ditemukan, TANPA data lain (backend VerifyCertificateDto sendiri sudah
 * struktural minim — lihat CertificateFlowTests.VerifyCertificate_ValidCode_Returns200Without
 * SensitiveFields, tepat 6 field). Server Component murni, TIDAK pakai notFound() (beda dari
 * p/[slug]) krn "tidak ditemukan" di sini adalah HASIL VALID halaman (✖ ditampilkan inline),
 * bukan error 404 Next.js — publik memang boleh mencoba kode sembarang & selalu dapat jawaban.
 */
export default async function VerifyCertificatePage({ params }: PageProps) {
  const { code } = await params;
  const { status, data } = await publicFetcher<VerifyCertificateDto>(`/verify/${encodeURIComponent(code)}`, 0);

  const valid = status === 200 && data !== null;

  return (
    <main data-theme="sekolah" className="mx-auto max-w-md bg-surface p-6">
      {valid && data ? (
        <div className="flex flex-col gap-3 rounded-[var(--radius-lg)] border border-status-green/30 bg-status-green-bg p-6 text-center">
          <span className="text-3xl" aria-hidden="true">✔</span>
          <p className="text-lg font-semibold text-status-green">Sertifikat Terverifikasi</p>
          <dl className="mt-2 flex flex-col gap-1 text-left text-sm text-ink">
            <div className="flex justify-between"><dt className="text-ink-muted">Nama</dt><dd className="font-medium">{data.studentName}</dd></div>
            <div className="flex justify-between"><dt className="text-ink-muted">Sekolah</dt><dd className="font-medium">{data.schoolName}</dd></div>
            <div className="flex justify-between"><dt className="text-ink-muted">DUDI</dt><dd className="font-medium">{data.companyName}</dd></div>
            <div className="flex justify-between"><dt className="text-ink-muted">Periode</dt><dd className="font-medium">{data.periodLabel}</dd></div>
          </dl>
        </div>
      ) : (
        <div className="flex flex-col gap-2 rounded-[var(--radius-lg)] border border-status-red/30 bg-status-red-bg p-6 text-center">
          <span className="text-3xl" aria-hidden="true">✖</span>
          <p className="text-lg font-semibold text-status-red">Sertifikat Tidak Ditemukan</p>
          <p className="text-sm text-ink-muted">Kode &quot;{code}&quot; tidak cocok dengan sertifikat manapun.</p>
        </div>
      )}
    </main>
  );
}
