import Link from "next/link";
import { Icon } from "@/components/ui";

export const metadata = {
  title: "Undangan Mentor — Vokasia",
};

interface ValidateResponse {
  valid: boolean;
}

function InviteError({ message }: { message: string }) {
  return (
    <main data-theme="sekolah" className="flex flex-1 items-center justify-center bg-surface px-5 py-10">
      <section className="w-full max-w-md rounded-[var(--radius-lg)] border border-status-red/30 bg-status-red-bg p-6 text-center">
        <Icon name="warning" size={32} className="mx-auto text-status-red" />
        <h1 className="mt-4 min-w-0 [overflow-wrap:anywhere] text-xl font-semibold text-status-red">
          Undangan belum bisa digunakan
        </h1>
        <p className="mt-2 text-sm leading-6 text-ink-muted">{message}</p>
        <Link
          href="/"
          className="mt-6 inline-flex h-[var(--tap-min)] items-center justify-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] bg-surface px-5 text-sm font-medium text-ink outline-none ring-1 ring-border hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
        >
          <Icon name="home" size={16} />
          Kembali ke beranda
        </Link>
      </section>
    </main>
  );
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
    return <InviteError message="Tautan tidak lengkap. Minta sekolah mengirim ulang undangan mentor." />;
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
    return <InviteError message="Tautan tidak valid atau sudah kedaluwarsa. Minta undangan baru ke sekolah." />;
  }

  return (
    <main data-theme="sekolah" className="flex flex-1 items-center justify-center bg-surface px-5 py-10">
      <div className="w-full max-w-md rounded-[var(--radius-lg)] border border-border bg-surface p-6 text-center">
        <h1 className="min-w-0 [overflow-wrap:anywhere] text-lg font-semibold text-ink">Undangan mentor pendamping PKL</h1>
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
          className="mt-4 inline-flex h-[var(--tap-min)] items-center justify-center gap-2 whitespace-nowrap rounded-[var(--radius-md)] bg-primary px-6 text-base font-medium text-primary-ink outline-none transition-opacity hover:opacity-90 focus-visible:outline-2 focus-visible:outline-focus focus-visible:outline-offset-2 active:translate-y-px"
        >
          Masuk sebagai mentor
        </a>
      </div>
    </main>
  );
}
