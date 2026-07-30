# VOK-H6-E2 — UI Superadmin (W5) + portofolio publik (W6) + verify + billing UI

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-2 `frontend/` | `h6-eng2-sa-portfolio-ui` | GPT-5.3-Codex | medium | **M5** | PRD §4.3 W5–W6, NFR-PERF-01 |

## Tugas

Permukaan Superadmin (W5), halaman publik portofolio (W6) + verifikasi sertifikat — halaman publik harus sangat ringan (LCP <2,5 dtk 3G) — plus editor portofolio siswa dan billing sekolah.

## Implementasi

### 1. `/sa` (W5, desktop-first)
- `sa/page.tsx` — tujuan: KPI cards (`GetPlatformKpis`) + panel SYSTEM HEALTH (`GetSystemHealth`: queue, DLQ, jobs gagal, p95, disk) dengan indikator ✅/⚠.
- `sa/tenants/page.tsx` + `TenantWizard({onCreated})` — tujuan: tabel tenant (cari, plan, status, ⋮ kelola) + wizard 3 langkah: data sekolah → pilih plan → admin pertama (`CreateTenant`).
- `sa/dudi/page.tsx` + `MergeCompanyDialog({sourceId})` — tujuan: registry global: verifikasi usulan, cari duplikat, merge dengan preview dampak (jumlah placement pindah) + riwayat.
- `sa/plans/page.tsx` — tujuan: CRUD plan + toggle feature flags + override per tenant.
- `sa/invoices/page.tsx` — tujuan: daftar invoice semua tenant; lihat bukti transfer (preview objek) → `ConfirmPayment`.
- `sa/audit/page.tsx` — tujuan: viewer audit: filter aktor/entitas/tanggal + pagination.

### 2. Publik (ultra-ringan: Server Components murni, tanpa JS berat, gambar thumbnail, cache)
- `p/[slug]/page.tsx` — tujuan: W6: identitas (tanpa kontak/NISN), KOMPETENSI TERVERIFIKASI (dari n jurnal approved), sampel foto (thumbnail, lazy), badge sertifikat → link verify; `generateMetadata` OG tags; 404 elegan bila unpublished.
- `verify/[code]/page.tsx` — tujuan: hasil `VerifyCertificate`: ✔ terverifikasi (nama, sekolah, DUDI, periode) / ✖ tidak ditemukan; tanpa data lain.

### 3. Siswa & sekolah
- `student/portofolio/page.tsx` + `SamplePicker({approvedJournals, selected, max:6})` — tujuan: editor: headline, pilih sampel, preview publik; toggle **Publikasikan** dengan consent copy jelas ("dapat dilihat siapa pun, tanpa kontak/NISN") + Unpublish.
- `app/billing/page.tsx` — tujuan: TenantAdmin: daftar invoice + status; upload bukti transfer (presigned) → status `ProofUploaded`.

## Acceptance Criteria

- Given siswa publish, When buka `/p/{slug}` incognito, Then W6 tampil; grep source page → tidak ada NISN/email/telepon.
- Given Lighthouse mobile 3G `/p/{slug}`, Then LCP <2,5 dtk (angka dilampirkan).
- Given wizard tenant selesai, Then tenant muncul di tabel + admin menerima undangan (dev inbox).
- Given bukti transfer diupload, Then SA melihat & konfirmasi → status Paid di kedua sisi.

## DoD + verifikasi runner (medium)

`bun run build` → Lighthouse `/p/{slug}` (throttling 3G) lampirkan → smoke wizard + merge + publish/unpublish → screenshot W5 & W6 → setor.
