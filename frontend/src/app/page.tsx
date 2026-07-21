import { Button } from "@/components/ui";

/** Landing ringkas (PRD sitemap: / → landing + login). Tombol Masuk diaktifkan penuh di H2-E2. */
export default function LandingPage() {
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-4 p-6 text-center">
      <h1 className="text-2xl font-semibold text-ink">Vokasia</h1>
      <p className="max-w-sm text-sm text-ink-muted">
        Platform manajemen PKL untuk SMK — jurnal harian, monitoring, penilaian, dan sertifikat
        dalam satu tempat.
      </p>
      <a href="/login">
        <Button size="lg">Masuk</Button>
      </a>
    </main>
  );
}
