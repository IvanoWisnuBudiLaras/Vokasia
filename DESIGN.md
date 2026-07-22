# DESIGN.md — Kontrak Visual Vokasia

**STATUS: BEKU.** Disetujui Dev 2026-07-21 (chat: "approve") — lihat DECISIONS.md D20. Tokens & voice di bawah diisi via metodologi hallmark (skill nyata `nutlope/hallmark`, terinstal manual sesi ini krn tak tersedia sbg skill Cowork bawaan — lihat DECISIONS.md D18 utk alasan genre/tone/anchor hue + verifikasi kontras WCAG; D19 utk penerapan komponen di `/app` dashboard). Perubahan dari titik ini = change control PRD 3.6 (butuh entry DECISIONS.md baru, bukan edit diam-diam).

## Beku dari PRD (berlaku sekarang, tidak menunggu hallmark)

- Mobile-first `/student` `/mentor` (Android murah, 3G, layar 360px); desktop-first `/app` `/sa` (PRD 4.1, NFR-UX-03).
- Target sentuh ≥44px; bahasa sederhana; initial payload `/student` <200KB (NFR-UX-02, NFR-PERF-05).
- Warna status konsisten semua surface: 🟢 beres · 🟡 perlu perhatian · 🔴 bermasalah.
- Setiap layar wajib punya state: loading / empty / error / offline. Tanpa layar buntu (NFR-UX-04).
- Wireframe W1–W6 (PRD 4.3) = kontrak layout mengikat ENG-2.
- Design-in-code: Tailwind + design tokens. Tanpa fase Figma di MVP.
- Portofolio publik: LCP <2,5 dtk di 3G (NFR-PERF-01) — hemat asset, no heavy hero.

## Tokens — `frontend/src/app/globals.css` adalah sumber kebenaran; ringkasan di bawah

- Genre · **modern-minimal** (institusional/B2B multi-tenant, bukan konsumer — hallmark SKILL.md genre-detection: "SaaS, enterprise, platform, B2B" → modern-minimal)
- Tone · **utilitarian**, warna teknis (fungsional, bukan editorial/luxury/playful) — cermin brief hallmark sendiri di §3 file ini
- Theme route · **custom (tuned)**, BUKAN salah satu 20 tema katalog — font Geist/Geist Mono sudah dipakai sejak H1-E2 (pre-flight scan hallmark: "preserve font stack" adalah prioritas #1 sebelum tanya apa pun); custom-tuned mempertahankan itu, hanya palet yang di-tuning penuh ke OKLCH

### Colors

Anchor hue **222°** (teal-biru) — bukan biru generik ~250–265° yang dipakai hampir semua SaaS/AI (hallmark `color.md` eksplisit: *"Most AI-generated UI fails on colour. It picks blue."*). Dipilih karena: (a) tetap terbaca tepercaya/institusional utk konteks sekolah, (b) beda jelas dari hue status RAG (hijau ~155° / kuning ~75–85° / merah ~25°) — warna brand tak pernah rancu dgn status di dashboard padat-data, (c) chroma rendah–sedang (0,10–0,16) sesuai tone utilitarian, bukan luxury/playful.

```css
--color-surface:        oklch(98% 0.004 222);  --color-primary:      oklch(48% 0.12 222);
--color-surface-muted:  oklch(95% 0.006 222);  --color-primary-ink:  oklch(99% 0.003 222);
--color-ink:            oklch(19% 0.014 222);  --color-primary-muted: oklch(94% 0.025 222);
--color-ink-muted:      oklch(46% 0.020 222);  --color-focus:        oklch(58% 0.15 222);
--color-border:         oklch(62% 0.016 222);
```

### Tema Sekolah (scoped, D31 — change control atas freeze D20)

Institusional 222° di atas **tetap** untuk `/app` `/sa` (dashboard desktop). Untuk shell mobile
`(student)` `(mentor)` + halaman publik (`login`, `verify/[code]`, `p/[slug]`, `mentor-invite`) —
tema baru diminta user: warna sekolah #4ED7F1 / #FFFDF6(bg) / #6FE6FC / #A8F1FF, diterapkan via
`[data-theme="sekolah"]` scoped di `globals.css`, **bukan** menimpa `:root`. Lihat DECISIONS.md
D31 untuk rasional penuh + tabel kontras WCAG.

Ringkasan: keempat warna asli (semua terang, L 82–99%) dipertahankan apa adanya sebagai
surface/primary-muted/accent-bright/accent-light — **dekoratif/non-teks saja**. Teks & elemen
fungsional (ink/primary/border/focus) diturunkan pada hue yang sama (~213°, cyan — turunan
langsung dari #4ED7F1) di lightness lebih rendah, seluruhnya diverifikasi ≥ ambang WCAG:

```css
[data-theme="sekolah"] {
  --color-surface:        oklch(99% 0.009 94);    --color-primary:       oklch(50% 0.14 213);
  --color-ink:             oklch(18% 0.03 213);    --color-primary-ink:  oklch(99% 0.006 213);
  --color-ink-muted:       oklch(48% 0.03 213);    --color-primary-muted: oklch(91.5% 0.074 210.8); /* =#A8F1FF */
  --color-border:          oklch(65% 0.06 213);    --color-focus:        oklch(55% 0.18 213);
  --color-accent-bright:   oklch(81.6% 0.121 212.8); /* =#4ED7F1 asli */
  --color-accent-light:    oklch(86.4% 0.111 211.4); /* =#6FE6FC asli */
}
```

RAG (hijau=beres/kuning=perhatian/merah=bermasalah) **beku dari PRD, tidak berubah maknanya** — hanya representasi OKLCH-nya diperbaiki. Semua pasangan teks/latar di atas **diverifikasi programatik** (OKLCH→sRGB→WCAG, bukan diasumsikan) terhadap target `color.md`: body ≥4,5:1, batas UI/border ≥3:1 — lihat DECISIONS.md D18 utk tabel lengkap. Temuan penting: token border/`--color-border` LAMA (hex placeholder) gagal kontras (1,3:1, jauh di bawah 3:1 WCAG 1.4.11) — ini perbaikan aksesibilitas nyata, bukan sekadar estetika.

### Typography

Geist + Geist Mono (next/font, sudah jalan sejak H1-E2 — **dipertahankan**, bukan diganti). Kontras hierarki lewat **bobot**, bukan famili kedua — sah menurut hallmark utk tone teknis/utilitarian ("Technical" tone table: Geist 700 display atas Geist 400 body). Body 400, judul ≥700 (beda ≥300 unit sesuai `typography.md`). Skala 1,25 (major third) dari basis 16px; badan teks minimum 16px (aksesibilitas + `/student` NFR-UX-02). Tanpa huruf miring pada judul (aturan anti-slop hallmark — miring hanya di dalam paragraf).

### Spacing · Radius · Elevation

Skala 4pt bernama-peran: `--space-1`(4px) … `--space-12`(64px). Radius 3 tingkat: `sm`(6px)/`md`(10px)/`lg`(16px). Elevation via **bobot & warna**, bukan bayangan berlapis (hallmark `layout-and-space.md`) — hindari `box-shadow` bertumpuk; kalau perlu, satu bayangan tipis (`whisper`) saja.

### Motion

Stance **motion-cut** (tanpa pustaka animasi terpasang — cermin batasan beku "minimal, tanpa animasi berat di 3G"). Tiga easing bernama (`--ease-out/-in/-in-out`) + tiga durasi (120/200/320ms) — cukup utk transisi state dasar (hover, fokus, buka/tutup), TIDAK utk halaman/hero. `prefers-reduced-motion` dihormati global (lihat globals.css). Animasikan `transform`/`opacity` saja.

## Voice & copy

Bahasa Indonesia sederhana setara siswa SMK. Dilarang jargon. Label aksi konkret ("KIRIM JURNAL", bukan "Submit entry").

Prinsip (adaptasi `copy.md` hallmark ke konteks aplikasi fungsional, bukan landing page):
- **Kata kerja spesifik pada tombol.** "Simpan jurnal", "Kirim penilaian", "Undang mentor" — bukan "OK"/"Submit"/"Lanjut" tanpa konteks.
- **Error = instruksi, bukan permintaan maaf.** Urutan: apa yang salah → kenapa (kalau tahu) → apa yang harus dilakukan. Contoh: "Password salah 3 kali. Akun dikunci 5 menit. Coba lagi setelah itu." — bukan "Oops, terjadi kesalahan!"
- **Empty state 3 baris:** (1) apa yang kosong, (2) kenapa itu penting, (3) satu tombol aksi. Contoh: "Belum ada siswa terdaftar. Data siswa dipakai utk penempatan PKL & jurnal harian. [Tambah siswa]"
- **Konsisten satu istilah.** Pilih "Hapus" ATAU "Buang" — bukan campur. Sudah ada di kode: "Keluar" (logout), "Masuk" (login) — pertahankan, jangan ganti jadi "Logout"/"Sign in" di tempat lain.
- **Dilarang basa-basi marketing** — "Rasakan kemudahan...", "Solusi terbaik utk...", tanda seru di error, humor di alur gagal (lupa password, akun dikunci).
- Placeholder data uji **eksplisit palsu** ("Siswa Contoh", bukan nama asli siapa pun) — tak pernah metrik/testimoni karangan (hallmark: *"no fabricated content"*).

## Aturan pemakaian

1. ENG-2 dilarang hardcode warna/font/spacing di luar tokens.
2. Komponen inti dibuat H1 (WBS 1.5) dan dipakai ulang: `Button`, `Input`, `Card`, `StatusBadge` (RAG), `EmptyState`, `ErrorState`, `OfflineBanner`.
3. Brief hallmark = **app fungsional PKL**, bukan landing page marketing. Prioritas: kecepatan, keterbacaan, form ergonomis — bukan dekorasi.
