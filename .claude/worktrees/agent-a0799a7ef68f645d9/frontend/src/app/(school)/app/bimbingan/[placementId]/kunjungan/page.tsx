import { ErrorState } from "@/components/ui";
import { fetcher } from "@/lib/fetcher";
import type { Paged, PlacementDto, StudentDto } from "@/lib/apiTypes";
import { VisitSection } from "./VisitSection";

export const dynamic = "force-dynamic";

/**
 * VOK-H5-E2 §1 app/bimbingan/[placementId]/kunjungan/page.tsx — halaman kunjungan monitoring guru
 * ke DUDI (W4, mobile-first — guru mengisi di HP saat kunjungan langsung). Nama siswa diambil via
 * `/students?pageSize=1000` + cari by id (TIDAK ada `GET /students/{id}` tunggal di backend, gap
 * yang SAMA persis dgn precedent `app/bimbingan/page.tsx` — 1 fetch tambahan diterima, bukan
 * endpoint baru lagi). Nama perusahaan (DUDI) SENGAJA tidak ditampilkan — backend tak punya
 * endpoint GET perusahaan tunggal maupun list sama sekali (dikonfirmasi grep, di luar cakupan
 * ticket ini utk ditambal).
 */
export default async function KunjunganPage({
  params,
}: {
  params: Promise<{ placementId: string }>;
}) {
  const { placementId } = await params;

  let studentName = "Siswa";
  let loadError = false;

  try {
    const placement = await fetcher<PlacementDto>(`/placements/${placementId}`);
    const students = await fetcher<Paged<StudentDto>>("/students?pageSize=1000");
    studentName = students.items.find((s) => s.id === placement.studentId)?.fullName ?? "Siswa";
  } catch (err) {
    console.error("[kunjungan] gagal memuat data placement:", err);
    loadError = true;
  }

  return (
    <div className="mx-auto flex max-w-3xl flex-col gap-4">
      <div>
        <a href={`/app/bimbingan?placementId=${placementId}`} className="text-sm text-primary hover:underline">
          ← Kembali ke Bimbingan
        </a>
        <h1 className="mt-1 text-xl font-semibold text-ink">Kunjungan DUDI — {studentName}</h1>
        <p className="text-sm text-ink-muted">Catat kunjungan monitoring ke tempat PKL siswa.</p>
      </div>

      {loadError && <ErrorState message="Data placement belum bisa dimuat, tapi kamu tetap bisa mencatat kunjungan." />}

      <VisitSection placementId={placementId} />
    </div>
  );
}
