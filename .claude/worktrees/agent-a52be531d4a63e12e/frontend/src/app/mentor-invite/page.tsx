import { ErrorState } from "@/components/ui";

interface ValidateResponse {
  valid: boolean;
}

/**
 * VOK-H2-E3 §3 — halaman publik (TANPA sesi apa pun; sengaja di LUAR route group (mentor) yang
 * digerbangi proxy.ts, lihat matcher-nya: hanya /sa /app /mentor /student, bukan /mentor-invite).
 *
 * Perantara wajib antara email/WA berisi magic link dan konsumsi token sesungguhnya: panggil
 * ValidateMagicToken (backend, TANPA konsumsi) dulu di sini, baru kalau valid tampilkan tombol
 * eksplisit ke /api/auth/magic-link (yang BARU MENGKONSUMSI token). Alasan dua-langkah ini bukan
 * dekorasi UX — banyak klien email/pemindai keamanan otomatis MENGUNJUNGI tautan di dalam email
 * (link-preview/prefetch), yang kalau tautan itu langsung mengarah ke endpoint pengkonsumsi token
 * sekali-pakai, token akan "terbakar" sebelum mentor sungguhan sempat klik. Endpoint validate
 * tidak punya efek samping (aman dikunjungi bot), endpoint exchange hanya tersentuh lewat klik
 * asli manusia di tombol bawah.
 */
export default async function MentorInvitePage({
  searchParams,
}: {
  searchParams: Promise<{ token?: string }>;
}) {
  const { token } = await searchParams;

  if (!token) {
    return (
      <main data-theme="sekolah" className="mx-auto max-w-md bg-surface p-6">
        <ErrorState message="Tautan tidak lengkap — token tidak ditemukan di URL." />
      </main>
    );
  }

  const apiBase = process.env.API_INTERNAL_URL ?? "http://localhost:5000";
  let valid = false;
  try {
    const res = await fetch(
      new URL(`/api/mentor-invites/validate?token=${encodeURIComponent(token)}`, apiBase),
      { cache: "no-store" }
    );
    if (res.ok) {
      const data = (await res.json()) as ValidateResponse;
      valid = data.valid;
    }
  } catch {
    valid = false; // API tak terjangkau -> perlakukan sama spt tautan tak valid, jangan crash halaman publik.
  }

  if (!valid) {
    return (
      <main data-theme="sekolah" className="mx-auto max-w-md bg-surface p-6">
        <ErrorState message="Tautan tidak valid atau sudah kedaluwarsa. Minta undangan baru ke sekolah." />
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-md p-6">
      <div className="rounded-[var(--radius-lg)] border border-border bg-surface p-6 text-center">
        <h1 className="text-lg font-semibold text-ink">Undangan mentor pendamping PKL</h1>
        <p className="mt-2 text-sm text-ink-muted">
          Anda diundang menjadi mentor pendamping siswa PKL. Klik tombol di bawah untuk masuk —
          tanpa perlu membuat password.
        </p>
        {/*
          Anchor gaya-tombol, BUKAN komponen <Button> (yang cuma render <button>, bukan <a>) —
          duplikasi sengaja className primer/lg Button.tsx utk SATU pemakaian ini drpd menambah
          polimorfisme href ke komponen inti Button yang sudah dipakai puluhan tempat lain sesi
          ini (D19); kalau kebutuhan "link bergaya tombol" muncul lagi di halaman lain, baru layak
          diekstrak jadi helper bersama.
        */}
        <a
          href={`/api/auth/magic-link?token=${encodeURIComponent(token)}`}
          className="mt-4 inline-flex h-[var(--tap-min)] items-center justify-center gap-2 rounded-[var(--radius-md)] bg-primary px-6 text-base font-medium text-primary-ink outline-none transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2"
        >
          Masuk sebagai mentor
        </a>
      </div>
    </main>
  );
}
