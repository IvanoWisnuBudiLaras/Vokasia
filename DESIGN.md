# DESIGN.md — Kontrak Visual Vokasia

**STATUS: SKELETON.** Bagian tokens & voice diisi via hallmark pra-H1 (`TOOLING.md` §3 langkah 3). Setelah terisi dan disetujui Developer → file ini **beku**; perubahan = change control (PRD 3.6).

## Beku dari PRD (berlaku sekarang, tidak menunggu hallmark)

- Mobile-first `/student` `/mentor` (Android murah, 3G, layar 360px); desktop-first `/app` `/sa` (PRD 4.1, NFR-UX-03).
- Target sentuh ≥44px; bahasa sederhana; initial payload `/student` <200KB (NFR-UX-02, NFR-PERF-05).
- Warna status konsisten semua surface: 🟢 beres · 🟡 perlu perhatian · 🔴 bermasalah.
- Setiap layar wajib punya state: loading / empty / error / offline. Tanpa layar buntu (NFR-UX-04).
- Wireframe W1–W6 (PRD 4.3) = kontrak layout mengikat ENG-2.
- Design-in-code: Tailwind + design tokens. Tanpa fase Figma di MVP.
- Portofolio publik: LCP <2,5 dtk di 3G (NFR-PERF-01) — hemat asset, no heavy hero.

## Tokens (TODO — output hallmark "lock the system")

### Colors
<!-- primary / surface / ink / status (green-amber-red match RAG) / semantic -->

### Typography
<!-- font stack (system-first untuk low-data), scale, weight -->

### Spacing · Radius · Elevation
<!-- skala terbatas; konsisten -->

### Motion
<!-- minimal; hormati prefers-reduced-motion; tanpa animasi berat di 3G -->

## Voice & copy (TODO — hasil hallmark, disesuaikan)

Bahasa Indonesia sederhana setara siswa SMK. Dilarang jargon. Label aksi konkret ("KIRIM JURNAL", bukan "Submit entry").

## Aturan pemakaian

1. ENG-2 dilarang hardcode warna/font/spacing di luar tokens.
2. Komponen inti dibuat H1 (WBS 1.5) dan dipakai ulang: `Button`, `Input`, `Card`, `StatusBadge` (RAG), `EmptyState`, `ErrorState`, `OfflineBanner`.
3. Brief hallmark = **app fungsional PKL**, bukan landing page marketing. Prioritas: kecepatan, keterbacaan, form ergonomis — bukan dekorasi.
