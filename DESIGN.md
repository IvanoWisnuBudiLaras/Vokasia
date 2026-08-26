# DESIGN.md — Kontrak Visual & UX Vokasia V2.1

**STATUS: DRAFT — Visual Architecture V2.1**
Dokumen kontrak visual & UX yang komprehensif. Terbuka untuk pengembangan dan revisi.

---

## 1. Status & Ruang Lingkup

Dokumen ini adalah kontrak Visual Architecture & UX V2.1 yang lengkap dan efektif. Dokumen ini menggantikan seluruh aturan visual sebelumnya dengan satu sumber kebenaran yang koheren.

**PRD Wireframes** tetap mengikat untuk:
- Informasi wajib
- Aksi wajib
- Urutan workflow
- Visibilitas otorisasi
- Status aplikasi (loading, empty, error, offline)
- Perilaku domain bisnis

**PRD Wireframes TIDAK mengikat** untuk:
- Tata letak piksel
- Margin, border, kartu
- Komposisi visual
- Max-width halaman

---

## 2. Filosofi Desain V2.1

### Genre: Clean Coastal + Focused Functional UX

Vokasia memposisikan diri sebagai platform yang:
- **Bersih** (clean white dominant)
- **Tenang** (calm, not overwhelming)
- **Modern** (soft modern shapes)
- **Terfokus** (information-first, not decoration-first)
- **Profesional** (trustworthy, restrained)
- **Ringan** (lightweight, performant)

Konsep visual: **"White Beach + Mediterranean Sea"**

### Karakter yang Dilarang

Platform ini **TIDAK BOLEH** menyerupai:
- Sistem administrasi sekolah tradisional (SIAKAD/dinas jadul)
- ERP kaku era 2010-an
- Dashboard generik Tailwind/Bootstrap default
- Template AI SaaS/Web3/kripto
- Demo Material Design bawaan tanpa modifikasi
- Glassmorphism berat
- Neon/lime gradients
- Corporate navy dashboards
- Card-wall dashboards

### Sumber "Cool" Quality

Kualitas visual datang dari:
- Komposisi dan proporsi
- Spacing dan rhythm
- Elevation selektif
- Microinteraction halus
- Dekorasi ringan bermanfaat

BUKAN dari gradient besar, ilustrasi raksasa, card berlebihan, tipografi besar, atau animasi berat.

---

## 3. Sistem Warna V2.1

### Dominasi Visual

**80–90%**: Putih / neutral / konten
**10–20%**: Brand / tonal / semantic accents

### Brand Tokens

| Token CSS | Hex | OKLCH | Peran | Batasan Kontras |
|-----------|-----|-------|-------|-----------------|
| `--color-surface` | `#FFFFFF` | oklch(100% 0 0) | Canvas utama, card, container (Clean White) | — |
| `--color-surface-muted` | `#F8FAFC` | oklch(98% 0.005 250) | Tonal surface netral, subtle sections | — |
| `--color-brand-accent` | `#0284C7` | oklch(60.1% 0.165 243.3) | Mediterranean Azure — accent, focus, selected, icon detail | **Dilarang** untuk teks normal putih (4.10:1, FAIL AA) |
| `--color-brand-action` | `#0369A1` | oklch(50.4% 0.162 243.3) | Marine Blue — CTA background, strong interaction | **Wajib** untuk teks putih normal (5.93:1, PASS AA) |
| `--color-brand-strong` | `#0369A1` | oklch(50.4% 0.162 243.3) | Hover/pressed state (sama dengan action) | — |
| `--color-brand-soft` | `#F0F9FF` | oklch(96.3% 0.021 243.3) | Sea Mist — tonal container, selected background, form fill | — |
| `--color-ink` | `#1C1D1B` | oklch(19% 0.003 110) | Teks utama, ikon gelap | — |
| `--color-ink-muted` | `#5F6B7A` | oklch(51% 0.03 250) | Teks sekunder, keterangan (cool slate, 5.43:1 vs white) | PASS AA |
| `--color-border` | `#E2E8F0` | oklch(90% 0.01 250) | Divider, border struktural | — |

### Aliases Semantik

```
--color-primary = --color-brand-action      (Marine Blue, accessible)
--color-primary-ink = #FFFFFF               (White text on primary)
--color-focus = --color-brand-accent        (Azure focus ring)
```

### Warna Semantik (RAG)

Independen dari brand:
- **Green (Success)**: `oklch(65% 0.15 145)` — beres, approved, complete
- **Amber (Warning)**: `oklch(75% 0.15 75)` — perlu perhatian, pending concern
- **Red (Danger)**: `oklch(55% 0.20 25)` — bermasalah, rejected, critical
- **Blue (Info)**: `oklch(60% 0.12 250)` — informasi saja

### Larangan Warna

- **Dilarang** menggunakan `#D7F24B` (Acid Pear) dalam bentuk apapun
- **Dilarang** menggunakan `#F7F7F2` (Warm Paper/Sand) sebagai canvas global
- **Dilarang** menggunakan warna brand sebagai pengganti semantic status
- **Dilarang** hardcoded hex di luar file token global

---

## 4. Tipografi

### Font Family

- **Geist** (sans-serif): UI operasional, konten utama
- **Geist Mono**: ID unik, kode, log, metrik teknis

### Skala Tipografi

| Elemen | Ukuran | Karakter |
|--------|--------|----------|
| Page Title | 28–32px | Jelas tapi tenang, bukan hero raksasa |
| Section Heading | 18–22px | Informatif, hierarchy jelas |
| Body | 15–16px | 16px minimum untuk mobile siswa |
| Metadata | 13–14px | Keterangan sekunder |

### Prinsip

- Informasi mendominasi, bukan tipografi dramatis
- Hindari uppercase eyebrow headings
- Hindari tracking (spasi huruf) berlebihan
- Hindari variasi weight yang tidak perlu
- Hindari italic pada heading
- Helper text hanya jika user berpotensi salah

---

## 5. Tata Ruang, Layout & Navigasi

### Spacing

- Basis 4px grid
- Tailwind utilities diizinkan jika map ke 4pt scale
- Hindari arbitrary spacing tanpa justifikasi teknis

### Layout Strategy: Wide-First Hybrid

- **Operational desktop**: gunakan viewport dengan cerdas
- **Public/reading**: boleh constrain reading width
- **Dense tables/reporting**: boleh lebar
- Jangan paksa semua halaman melalui satu max-width sempit

### Sidebar Desktop

- Collapsible (default expanded ~256px, collapsed ~64–72px)
- Expanded: icon + label
- Collapsed: icon rail + tooltip/aria-label
- Ingat state collapsed per device
- **Dilarang** sidebar biru penuh — white/neutral structure

### Topbar & Page Header

- **Global topbar**: minimal
- **Page header**: title + context + satu primary action (jika relevan)
- **Sticky header**: hanya untuk halaman operasional panjang (Admin, Reporting)

### Breadcrumbs

Gunakan hanya saat navigasi hierarki bermakna. Jangan tambahkan ke halaman shallow sebagai dekorasi.

### Navigasi Mobile

Untuk persona dengan ≤5 destinasi utama sederhana (mis. Siswa / Mentor), **bottom navigation MAY be used** jika sesuai secara bersih dengan arsitektur yang ada.

Namun:
- **DILARANG** melakukan refactoring navigasi berisiko hanya untuk memaksakan bottom navigation.
- Jika navigasi mobile yang ada sudah stabil, pertahankan dan tingkatkan perlakuan visualnya.
- Keselamatan dari regresi fungsional selalu lebih utama daripada memaksakan pola navigasi visual.

---

## 6. Komposisi Antarmuka & Structural Grammar

### Card Contract

Card adalah **BUKAN** default container.

Gunakan card untuk:
- Objek terikat mandiri (bounded object)
- Unit interaktif mandiri
- Layer yang terangkat (elevated layer)
- Konteks keputusan terisolasi

**Prioritas separator**:
1. Whitespace
2. Heading
3. Alignment
4. Divider
5. Tonal surface
6. Card (hanya jika benar-benar perlu)

### Card Static vs Interactive

- **Static card**: flat, tanpa shadow, tanpa hover animation
- **Interactive card**: subtle tactile elevation, hover shadow ringan

Gunakan prop/variant untuk membedakan — jangan animasikan semua card.

### Border

Border harus intentional:
- Batas input form
- Pembatas tabel
- State aktif
- Pemisahan aksesibilitas

Jangan jadikan border sebagai mekanisme utama information architecture.

### Detail Navigation Grammar (Panel vs Halaman)

- **Desktop (Informasi Cepat / Ringkas)**: gunakan right detail panel.
- **Desktop (Workflow Kompleks / Edit / Intervensi / Riwayat Panjang)**: gunakan halaman penuh (full page).
- **Mobile**: selalu gunakan halaman penuh (full page).

*Contoh penggunaan right detail panel desktop*:
- Teacher → Ringkasan cepat siswa
- SuperAdmin → Ringkasan cepat tenant
- SuperAdmin → Ringkasan cepat user

---

## 7. Global Component Grammars

### A. Global Icon Grammar

Ikon adalah **VISUAL HOOKS**, bukan sumber informasi tunggal.

- **Navigasi**: ikon + teks
- **Aksi**: teks utama, ikon opsional
- **Icon-only**: hanya untuk aksi ringkas yang secara universal mudah dipahami (mis. close `x`, search, overflow `⋯`).
- **Wajib Aksesibilitas**: Setiap kontrol icon-only WAJIB memiliki `aria-label` dan tooltip jika relevan.
- **Dilarang** membuat lingkaran background warna-warni pada setiap ikon, kecuali ikon tersebut memerlukan penekanan fungsional penting.

### B. Global Status Grammar

Default status representation: **TEKS + WARNA SEMANTIK**

- Status penting dapat ditambahkan ikon.
- **Dilarang** mengomunikasikan status hanya melalui warna tanpa teks/label.
- Jangan jadikan setiap status sebagai pill/badge kecuali kepadatan konteks memang membutuhkannya.
- Warna semantik tetap: Green (Success), Amber (Warning), Red (Danger), Blue (Info).

### C. Global Search & Filter Grammar

- **Halaman Sederhana**: pencarian hanya jika berguna.
- **Halaman Data Padat**:
  ```
  Search + 2–3 filter utama + "Filter lainnya"
  ```
- **Dilarang** menampilkan panel filter raksasa secara default.
- **Pelaporan**: filter periode selalu terlihat jika relevan.

---

## 8. Shape, Elevation & Motion

### Radius

**Soft Modern**: 10–16px tergantung peran komponen.

Jangan otomatis pill-shaped. Rounded-pill hanya untuk komponen yang semantiknya sesuai.

### Shadow

**Crisp Blue-Tinted Elevation**:
- Controlled, slightly directional
- Readable depth, subtle blue family
- Bukan glow, neon, atau blur besar
- Bukan hard black shadow

**Penggunaan**:
- **Primary CTA**: small tactile elevation
- **Interactive card**: lighter calm elevation
- **Dialog/popover**: stronger layer separation
- **Input**: normally no shadow
- **Navigation**: normally no shadow

### Motion

**Hybrid & Performant**:
- Primary CTA: hover sedikit naik, shadow berubah, pressed turun
- Cards/navigation: calmer transitions
- Dialogs: clear but restrained elevation transition

**Gunakan**: transform, opacity, box-shadow
**Hindari**: particles, looping animation, video backgrounds, heavy blur

**Selalu hormati**: `prefers-reduced-motion`

---

## 9. Komponen Primitif & Form

### Button

**Per context: ONE primary action.**

Secondary actions: secondary button/link
Rare actions: overflow menu (...)

**Primary CTA**:
- Background: `--color-brand-action` (Marine Blue)
- Text: white (5.93:1 contrast)
- Shadow: crisp blue-tinted
- Hover: slight lift + shadow change
- Active: pressed down

**Mobile**: touch-friendly (min 44px)
**Desktop**: more compact

### Input & Textarea

**Default: Light-Filled**

- **Fill**: subtle Sea Mist (`--color-brand-soft`) atau cool neutral tonal (`--color-surface-muted`)
- **Border**: border struktural yang jelas (`--color-border`)
- **Hover**: respon border/fill yang halus
- **Focus**: ring focus biru yang jelas (`--color-brand-action`)
- **Error**: border/ring merah + pesan inline
- **Disabled**: terdisabilitas secara visual namun tetap terbaca
- **Shadow**: umumnya tanpa shadow

### Form Error Behavior

- Validation error: inline near field
- System/network error: toast/banner

### Global Action Feedback

- Success/read-only toast: ~1 detik
- System error toast: lebih lama
- Reversible action: execute → toast → "Urungkan" (8–10 detik)
- Irreversible action: confirm BEFORE execution
- Critical action: konfirmasi lebih kuat + alasan jika didukung audit

---

## 10. Dialog, Loading & Empty States

### Dialog / Sheet

- **Desktop**: centered dialog untuk interaksi singkat
- **Mobile**: bottom sheet untuk interaksi singkat
- **Long/complex form**: page atau wizard, BUKAN dialog

### Loading

- Page/list dengan struktur known: skeleton
- Small async button: button spinner
- Hindari full-screen spinner jika struktur sudah diketahui

### Empty State

- Short
- Persona-aware
- Reason-aware
- Actionable when appropriate
- No giant illustration required

---

## 11. Notifikasi & Coach Marks

### Notification Routing

Notifikasi hanya untuk event IMPORTANT. Ordinary activity tetap di halaman relevan.

Pattern: Bell → compact panel → "Lihat semua" (dengan "Tandai semua dibaca").

**Routing Spesifik**:
- **Notifikasi Revisi Jurnal Siswa**: membuka jurnal terkait secara langsung.
- **Notifikasi Jurnal Disetujui**: membuka halaman jurnal dan menyorot status/konteks yang baru disetujui.

### First-Use Coach Marks

**Coach marks pada UI asli** — BUKAN slideshow terpisah.

- **Action-focused coach mark**: user dapat berinteraksi langsung dengan kontrol ASLI yang di-highlight.
- **Informational coach mark**: gunakan tombol "Berikutnya".
- Maksimal 3–5 langkah. Bisa dilewati atau dibuka ulang dari Help.
- Persistence: prefer account-side state, local storage sebagai supplement.

---

## 12. Wizard & Long Form

### Form Pendek

Single page.

### Form Panjang/Kompleks

Step-by-step wizard.

- **Desktop**: horizontal stepper
- **Mobile**: active step name + progress indicator

**Back behavior**:
- Android/gesture back: satu step backward
- Step 1: exit wizard
**Autosave**: per completed step saat tekan "Lanjut".
**Returning user**: resume last meaningful step.
---

## 13. Landing Page & Login

### Landing Page

Simple, informatif, tidak overwhelming.

- **Headline**: "PKL yang lebih terarah, terbukti, dan mudah dipantau."
- **Subheadline**: "Kelola penempatan, jurnal, bimbingan, penilaian, dan sertifikat dalam satu tempat."
- **Struktur**: Hero → PKL workflow → Persona summary → Verified outcomes → Final CTA
- **Visual**: White dominant, small blue accent, lightweight decoration, restrained typography.

### Login

- White canvas, compact form container, radius ~12–16px, crisp blue shadow, small logo, accessible Marine Blue CTA, light-filled input.
- **Flow**: Landing → Masuk → Authentication → Role resolved → Role workspace.

---

## 14. Student Workspace

### Prinsip

**"What do I need to do?"** — Task-first, mobile-friendly, low cognitive load, one clear action at a time.

### Student Home

**BUKAN KPI dashboard.**

- Top summary: PKL status, Company, Period, Industry mentor, Teacher.
- Progress: **CHECKLIST STAGES** (Penempatan ✓ → Jurnal aktif → Penilaian → Sertifikat). Tanpa persentase raksasa.
- Priority List: Simple list dengan semantic color hooks. Tap to expand inline (tonal background + crisp shadow). Max 3 revision items on home → "Lihat semua".

### Journal Page

- Primary: "Hari ini" (Status: No journal, Waiting, Revision, Approved).
- Journal History: Grouped by Hari ini, Kemarin, Minggu ini, older. Adaptive status filters.
- Create Journal: **BUKAN wizard**. Single fast page (Aktivitas → Deskripsi → Kompetensi → Evidence → Kirim). Continuous autosave ("Tersimpan").
- Journal Revision Model: Edit pada jurnal yang SAMA. Version history retained (mentor default: latest version).

### Evidence, Bimbingan, Penilaian

- Evidence: List show count ("3 bukti"). Expanded: thumbnails (image lightbox / detail view).
- Bimbingan: Timeline newest → oldest. Truncate long text. Actionable guidance gets workflow status ("Sudah dikerjakan" / "Konfirmasi selesai").
- Penilaian: Show components directly. Separate Mentor & Teacher comments. Final score ONLY when finalized.

---

## 15. Mentor Workspace

### Prinsip

**"What do I need to review?"** — Queue-first.

### Mentor Home & Queue

- Top summary: "12 menunggu · 3 revisi · Periode aktif 2026"
- Tabs: Menunggu, Revisi, Selesai. Priority: revisions returned → oldest waiting → newer.
- Journal Row: **LIST/TABLE-LIKE ROWS** (bukan card wall). Always-visible checkbox, student, date, summary, max 2 competencies + "+N", evidence count, status, quick approve.
- Review: "Setujui" (comment optional) & "Minta revisi" (comment mandatory). Action "Review berikutnya" (no auto-jump).
- Bulk Approve: Checkboxes always visible. Confirmation summary ("Setujui 8 jurnal dari 5 siswa?"). **Bulk revision NOT allowed**.

---

## 16. Teacher Workspace

### Prinsip

**Exception-first.** Focus on students requiring attention.

### Teacher Home & Roster

- Show: brief summary + students needing attention (inactive journal, unresolved revision, pending assessment/guidance).
- List row: severity strip, student, problem, duration, **ONE contextual action** (e.g., "Tinjau jurnal", "Beri catatan", "Nilai sekarang").
- Quick Detail: Desktop right panel, Mobile full page.
- Roster: Classic lightweight table (attention cases first, then alphabetical).

---

## 17. Tenant Admin Workspace

### Prinsip

Operations-first. Desktop-oriented but responsive.

### Home & Categories

- Textual summary: "120 siswa aktif di 18 DUDI · 7 hal perlu perhatian"
- Max ~5–7 priority issues on home.
- "Lihat semua" leads to grouped categories: **Placement**, **Pembimbing**, **Penilaian**, **Administrasi**. Each issue has ONE direct action.

### Data Tables & Wizards

- Pattern: Search + 2–3 key filters + "Filter lainnya" + "Tambah" → Table.
- Checkboxes always visible. Desktop bulk: toolbar above. Mobile bulk: sticky bottom.
- Placement Wizard: 5 steps (Siswa → DUDI → Pembimbing → Periode → Review & Simpan). Return to list + ~1s success toast.

### DUDI & Admin Mentor Detail

- DUDI List: Table with name, active students, active mentors, available slots, action.
- DUDI Detail: Header + operational sections + previews (max 5 students, max 3 mentors) + Slot copy ("8 dari 12 slot terisi · 4 tersedia").
- Admin Mentor Detail: Profile header (name, title, DUDI) + Operational context (students, pending journals, incomplete assessments, activity status). Condition + operational impact + one relevant action if problematic (bukan analytics dashboard).

---

## 18. Reporting UX

### Prinsip

Decision-oriented. Answer operational questions. Only domain-backed metrics.

**Dilarang** fabricated metrics (DUDI quality score, DUDI retention, AI risk/recommendations, synthetic impact).

### Structure & Export

- Home: Top findings/problems as simple text list (click → go to data).
- Detail Report: Short summary + main table + optional charts (max 1–2).
- Export: Separate PDF and XLSX buttons (Desktop: top-right, Mobile: above table).
- Empty state: Positive contextual ("Semua penilaian sudah selesai.").

---

## 19. SuperAdmin Workspace

### Prinsip

Platform-first. Desktop-heavy, high density.

### Dashboard Modules

Modular ~3-column desktop grid:
1. Platform problems (sorted by severity, then recency)
2. Service health (compact grid: API, Worker, Redis, RabbitMQ, PostgreSQL, MinIO)
3. Tenant attention (row: name, status, billing, users, main issue, action)
4. Billing ("12 unpaid · 3 overdue")
5. Background jobs ("128 selesai · 4 gagal · 2 retry")
6. Audit home (5–10 recent events)

### Tenant Detail Tabs

Full detail tabs: **Ringkasan**, **Billing**, **User**, **Usage**, **Audit**.
- **Ringkasan**: Tenant status, billing, users, main issue, service condition, recent activity, text-link shortcuts.
- **Billing**: Summary, problematic invoices, complete history.
- **User**: Table (name, role, status, last active, action). Quick actions: Ubah role, Aktifkan/Nonaktifkan. Role change shows impact summary → confirm.
- **Usage**: Domain-backed usage only (active users, storage, active student/mentor/teacher count, API/job activity). No raw technical noise.
- **Audit**: Important timeline first + "Lihat log lengkap" → Table with search, time range, actor filters.

### User Detail & Recovery

- User Detail Tabs: **Profil**, **Akses**, **Aktivitas**, **Audit**.
- Access tab: Role, key permissions, active sessions (device, location, last active, terminate session).
- Activity tab: Important timeline + link to full audit.
- Audit tab: Table + search, time range, action type.
- Account Recovery: **Self-service first**, Admin recovery as fallback.
- Sensitive Actions: **No impersonation**. Critical actions may require reason (stored in audit log). Normal actions require confirmation.

---

## 20. Public Portfolio

### Prinsip Through V3

**STANDARDIZED. NO visual/layout customization through V3** (deferred to V5).

### Structured Layout

- **Header**: NO student photo. Student name, school, major, PKL period, company, 1–2 sentence description.
- **Competencies**: Simple text list ("REST API · Database · Git · Testing"). Dilarang percentages, skill bars, stars, badge spam.
- **Evidence**: Max 4–6 principal items (thumbnail, title, one-line context). Deterministic system ordering. Lightbox for images, detail view for context.
- **Certificate Section**: Near bottom. Status, cert number, issue date, school, "Lihat Sertifikat", "Verifikasi". Footer: "Diverifikasi oleh Vokasia".
- **Share**: Single "Bagikan" action.

### Publication & Editing Workflow

- Statuses: Draft, Dipublikasikan.
- **Portfolio Overview**: Displays all structured sections.
- **Edit**: Opens focused section editor → returns to overview upon completion.
- Provide: "Lihat sebagai publik" button.
- Editing means maintaining structured data (not freeform layout building).
- Publish creates/updates draft until student explicitly clicks "Perbarui publikasi". Student may "Sembunyikan" (hiding portfolio MUST NOT hide/invalidate certificate verification).

---

## 21. Certificate & PDF

### Certificate Design

Formal Modern. White dominant, neutral typography, small blue accent. Professional in grayscale. No gradients, fake stamps, fake seals, or fake signatures. Domain-backed data only.

### Verification Page & Revocation

- Verification page is NOT a mini portfolio. Shows status, student, school, company, period, cert number, issue date, "Lihat Sertifikat", "Unduh PDF".
- Status: Valid (calm) vs Revoked (strong visual treatment). Never color alone.
- Revoke requires reason (Data salah, Diterbitkan keliru, Dibatalkan/tidak sah, Pelanggaran, Lainnya) + optional internal notes.
- Public Revoked Verification remains accessible, shows status "Dicabut" + public reason (hides internal notes). QR continues to resolve to page.

---

## 22. CV Through V3

### Prinsip

System-generated, deterministic, ATS-friendly, single column, real selectable text. **NO customization through V3.**

### Structure (Exact Order)

1. Nama + Kontak (Email, Phone, Vokasia Portfolio URL)
2. Pendidikan
3. Pengalaman PKL
4. Kompetensi Terverifikasi
5. Sertifikat + Link Verifikasi

### Explicit Exclusions

NO professional summary, NO about-me summary, NO Selected Work, NO final PKL score (excluded for ALL students), NO photo, NO skill bars/stars/percentages.

---

## 23. Anti-Slop Rules

Dilarang keras:
1. **Nested card walls**
2. **Eyebrow labels**
3. **Colored icon circles**
4. **Lime/neon gradients**
5. **Acid Pear glow**
6. **Fake verification badges**
7. **Fake charts/vanity numbers**
8. **Giant KPI cards**
9. **Excessive pills**
10. **Huge blue surfaces**
11. **Generic AI SaaS gradients**
12. **Decorative glow**
13. **Fabricated credentials**
14. **Unnecessary helper text**
15. **Inconsistent persona patterns**

---

## 24. Responsive Contract

### Breakpoints (Uji Wajib)

- **320/360px**: No horizontal scrollbar, readable IDs, no text overlap, reachable actions, touch targets ≥44px
- **375/414px**: Mobile standard
- **768px**: Tablet
- **1280px+**: Desktop

Review reference:
- Desktop: 1440x900
- Mobile: 390x844

Requirements: No horizontal scrollbar, readable IDs, no text overlap, reachable actions, touch targets ≥44px.

## 25. Visual Acceptance & Boundaries

- Implementation CANNOT be accepted from source code alone; real browser rendering must be reviewed for all major surfaces.
- Visual rewrite does NOT reopen backend architecture, authentication, authorization, BFF, tenant isolation, API/upload security, MinIO privacy, audit/worker/billing semantics, or domain calculations.

---

## 26. Future Scope & Exclusions

**Through V3, DILARANG:** Portfolio visual customization, CV customization, DUDI automated ranking, AI recommendations, Employment prediction, Curriculum intelligence, Complex impact scoring, Gamification, Social feed, Chat expansion, Impersonation, Vanity analytics.

---

## 27. Review Authority

- **Product Owner** owns subjective visual judgment (visual balance, blue shade feel, shadow intensity, spacing, aesthetics).
- **Engineering/Reviewer** validates objective criteria (accessibility, contrast, consistency, responsive behavior, security, privacy, E2E regression).

---

Dokumen ini terbuka untuk pengembangan dan revisi.
