# VOK-H1-E2 — Design tokens + 5 shell route group + komponen inti

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-2 `frontend/` | `h1-eng2-tokens-shells` | GPT-5.3-Codex | medium | M0 | PRD §4.1–4.3, DESIGN.md, AGENTS.md |

## Tugas

Fondasi visual: design tokens (dari DESIGN.md) sebagai satu-satunya sumber style, shell layout untuk 5 surface, dan 7 komponen inti reusable. Semua UI H2+ wajib memakai ini — tanpa hardcode.

## Implementasi

### 1. Tokens — `frontend/src/styles/tokens.css` + Tailwind config
- CSS variables dari DESIGN.md §Tokens: warna (primary, surface, ink, status green/amber/red), font scale, spacing, radius. Tujuan: satu sumber; Tailwind memetakan var, bukan nilai literal.
- Jika DESIGN.md §Tokens masih TODO (hallmark belum jalan): pakai nilai sementara netral + tandai `/* PROVISIONAL-TOKENS */` — struktur tetap final. `[ASSUMPTION]` wajib dicatat.

### 2. Route groups & layouts — `frontend/src/app/`
- `(sa)/sa/layout.tsx` · `(school)/app/layout.tsx` — tujuan: shell desktop-first (sidebar nav + header).
- `(mentor)/mentor/layout.tsx` · `(student)/student/layout.tsx` — tujuan: shell mobile-first (bottom nav, target sentuh ≥44px).
- `p/[slug]/page.tsx` + `verify/[code]/page.tsx` — tujuan: placeholder publik ringan (diisi H6).
- `page.tsx` (root) — tujuan: landing ringkas + tombol "Masuk" (login flow H2).
- Semua layout: Server Components; placeholder konten memakai `EmptyState`.

### 3. Komponen inti — `frontend/src/components/ui/`
- `Button({variant:'primary'|'secondary'|'danger', size:'md'|'lg', loading?, disabled?, children})` — tujuan: satu tombol seragam; `lg` untuk aksi utama mobile (≥44px).
- `Input({label, error?, hint?, ...native})` — tujuan: field berlabel + slot error dengan tinggi ter-reservasi (tanpa layout shift).
- `Textarea({label, maxLength, showCounter})` — tujuan: teks jurnal ≤500 kar + counter.
- `Card({title?, children, footer?})` — tujuan: kontainer konten standar.
- `StatusBadge({status:'green'|'amber'|'red', label})` — tujuan: RAG konsisten lintas surface (W2/W3).
- `EmptyState({icon?, title, description?, action?})` — tujuan: layar tanpa data tidak pernah kosong buntu.
- `ErrorState({message?, onRetry})` + `OfflineBanner()` — tujuan: state error/offline seragam (NFR-UX-04).

### 4. Util
- `cn(...classes)` — merge className. `fetcher(path, init?)` — wrapper fetch ke BFF (kerangka; auth H2).

## Acceptance Criteria

- Given repo clean, When `bun run build`, Then sukses tanpa type error.
- Given 5 segment dibuka, Then shell tampil + `EmptyState` (tanpa layar kosong).
- Given grep `#[0-9a-f]{6}` di `src/` (di luar tokens.css), Then nol hasil — semua warna via tokens.
- Given viewport 360px di `/student` `/mentor`, Then tanpa horizontal scroll; sentuh ≥44px.

## DoD + verifikasi runner (medium)

`bun install` → `bun run build` → jalankan grep hardcode → `git diff --stat` hanya `frontend/` → setor.
