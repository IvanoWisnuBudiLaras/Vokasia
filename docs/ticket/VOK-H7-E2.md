# VOK-H7-E2 — Sweep states lengkap + low-data + Lighthouse + PWA

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-2 `frontend/` | `h7-eng2-states-lowdata` | GPT-5.6 Luna | medium | **M6** | PRD NFR-UX-01..04, NFR-PERF-01/05, NFR-COMP-01 |

## Tugas

Polish rilis frontend: tidak ada satu pun layar tanpa state lengkap, `/student` <200KB initial, PWA installable, angka Lighthouse layak demo pilot.

## Implementasi

### 1. Sweep states — `frontend/docs/states-checklist.md`
- Tujuan: tabel SEMUA halaman (5 surface + publik) × 4 state (loading skeleton / empty / error+retry / offline) → isi ✓ per sel; sel kosong = kerjakan di ticket ini; hasil = bukti NFR-UX-04.
- `error.tsx` + `not-found.tsx` + `global-error.tsx` per segment — tujuan: error boundary seragam (ErrorState), bukan white screen.

### 2. Low-data `/student` (NFR-PERF-05)
- `next build` + analyzer — tujuan: initial JS `/student` <200KB: dynamic import (PhotoUploader, CompetencyPicker, SignaturePad), hapus dependency berat dari client bundle, ikon inline SVG (bukan lib penuh), font system-first.
- Gambar: semua thumbnail via `ThumbKey` (bukan original), `loading=lazy`, `sizes` benar.

### 3. PWA
- `manifest.webmanifest` + ikon (192/512, maskable) + `theme-color` — tujuan: installable di Android WebView/Chrome ≤2 th (NFR-COMP-01); shortcut "Isi Jurnal".
- Service worker minimal (app shell cache + fallback offline page) — tujuan: buka saat offline menampilkan halaman offline ramah (submit offline penuh = fase 2, JANGAN dikerjakan).

### 4. Audit akhir
- Lighthouse mobile (throttle 3G): `/student`, `/mentor`, `/p/{slug}` — target perf ≥85, a11y ≥90; hasil ke `frontend/docs/lighthouse-H7.md`.
- Audit sentuh ≥44px + kontras teks (a11y) + copy bahasa sederhana konsisten (label aksi konkret).

## Acceptance Criteria

- Given tabel states, Then 100% sel ✓ (tanpa pengecualian).
- Given bundle report, Then `/student` initial <200KB (screenshot analyzer dilampirkan).
- Given Chrome Android, Then PWA installable + buka offline → halaman offline ramah.
- Given Lighthouse 3 halaman, Then perf ≥85, a11y ≥90 (laporan dilampirkan).

## DoD + verifikasi runner (medium)

`bun run build` → analyzer + Lighthouse (simpan laporan) → uji offline & install di emulator/HP → setor checklist + angka.
