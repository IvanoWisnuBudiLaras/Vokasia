import { redirect } from "next/navigation";
import { Button } from "@/components/ui";
import { roleHome } from "@/lib/roleHome";
import { getSession } from "@/lib/session";

const ERROR_COPY: Record<string, string> = {
  unauthenticated: "Sesi berakhir. Silakan masuk kembali.",
  access_denied: "Kamu tidak punya akses ke halaman itu.",
};

interface LoginPageProps {
  searchParams: Promise<{ error?: string; next?: string }>;
}

/**
 * VOK-H2-E2 §app/login/page.tsx — halaman masuk: tombol submit form POST ke BFF
 * /api/auth/login (H2-E3, belum diimplementasi — lihat DECISIONS.md D15/D16). Copy sederhana +
 * state error dari query (?error=), sesuai AC ticket.
 */
export default async function LoginPage({ searchParams }: LoginPageProps) {
  const { error, next } = await searchParams;

  // Sudah login? Jangan tampilkan form lagi — langsung ke home role-nya. roleHome balik "/login"
  // utk role tanpa dashboard (ParentViewer) — biarkan lolos ke bawah, bukan redirect ke diri sendiri.
  const session = await getSession();
  if (session) {
    const home = roleHome(session.role);
    if (home !== "/login") {
      redirect(home);
    }
  }

  const nextParam = next ? `?next=${encodeURIComponent(next)}` : "";

  return (
    <main
      data-theme="sekolah"
      className="flex flex-1 flex-col items-center justify-center gap-4 bg-surface p-6 text-center"
    >
      <h1 className="text-2xl font-semibold text-ink">Masuk ke Vokasia</h1>

      {error && (
        <p role="alert" className="max-w-sm text-sm text-status-red">
          {ERROR_COPY[error] ?? "Terjadi kesalahan saat masuk. Coba lagi."}
        </p>
      )}

      <p className="max-w-sm text-sm text-ink-muted">
        Kamu akan diarahkan ke halaman masuk resmi. Kata sandi tidak pernah disimpan di perangkat
        ini.
      </p>

      {/* GET, bukan POST: kontrak BFF persis VOK-H2-E3 §1 ("GET /login -> handleLogin(req)") —
          memulai flow cuma navigasi/redirect-chain, bukan aksi yang mengubah state server. */}
      <a href={`/api/auth/login${nextParam}`}>
        <Button size="lg">Masuk</Button>
      </a>
    </main>
  );
}
