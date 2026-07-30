# VOK-H7-E3 — E2E Playwright 5 persona + laporan security final

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-3 `frontend/e2e` + docs | `h7-eng3-e2e-security` | GPT-5.4 Thinking | **ultra** | **M6** — input keputusan tag v0.1.0 | PRD NFR-MNT-03, NFR-SEC-01..08 |

## Tugas

Bukti akhir rilis: E2E 5 persona dari clean state tanpa intervensi manual + laporan security per butir NFR-SEC dengan bukti. Dua artefak ini = input gate M6; gagal = blocker rilis.

## Implementasi

### 1. E2E — `frontend/e2e/` (Playwright, `bunx playwright test`)
- `fixtures/setup.ts` — tujuan: global setup: compose up → migrate → `seed demo` → akun uji per persona; storageState per role (login sekali).
- `sa.spec.ts` — SuperAdmin: login → buat tenant via wizard → verifikasi DUDI usulan → lihat health → konfirmasi invoice (bukti transfer seed).
- `admin.spec.ts` — TenantAdmin: login → buat periode → import CSV kecil (fixture 5 baris, 1 rusak → error tampil) → placement + undang mentor → dashboard menampilkan KPI → finalize penilaian → rekap terkunci.
- `teacher.spec.ts` — Teacher: login → lihat bimbingan (siswa merah dari seed) → isi kunjungan + ttd → komentar jurnal → isi skor sekolah.
- `mentor.spec.ts` — Mentor: **buka magic link dari email dev inbox** (parse `.emails/`) → daftar pending → batch approve 5 → tolak 1 dengan alasan → isi skor industri.
- `student.spec.ts` — Student: login → isi jurnal (teks+kompetensi+1 foto fixture) → lihat status Approved setelah mentor flow → kurasi portofolio → publish → buka `/p/{slug}` sebagai anonim → verifikasi sertifikat via `/verify/{code}`.
- Prinsip: selector `data-testid` (tambahkan bila kurang — satu-satunya izin menyentuh wilayah E2, koordinasi via task ini); assertion async pakai auto-wait Playwright, tanpa sleep buta; trace on-failure.

### 2. Laporan security final — `backend/docs/security-report-v0.1.0.md`
- Tujuan: tabel NFR-SEC-01..08 → status LOLOS/GAGAL/WAIVED + **bukti konkret** per butir (nama test hijau, header response, hasil scan H6, screenshot devtools tanpa token, konfigurasi): 01 PKCE+15mnt+rotation → test; 02 no-token-in-browser → bukti devtools; 03 RBAC → RbacMatrixTests; 04 isolasi tenant+placement → TenantIsolationTests; 05 data anak (payload publik, EXIF strip, opt-in) → test + sampel; 06 validasi+presigned+rate limit → suites; 07 secrets+scan → laporan H6; 08 immutability → ImmutabilityTests.
- Item GAGAL → daftar blocker eksplisit ke Developer (bukan disembunyikan).

## Acceptance Criteria

- Given mesin dengan Docker + clean state, When `playwright test`, Then 5 persona lulus penuh tanpa intervensi; reproducible 2×.
- Given laporan security, Then 8 butir berstatus + bukti; tidak ada butir kosong.
- Trace/video failure tersimpan untuk debugging (artefak CI-ready).

## DoD + verifikasi runner (ultra)

Clean state penuh (`down -v` → up → seed) → suite E2E 2× → kompilasi laporan security (tarik bukti dari suite H2/H3/H5/H6) → PROMPT D → setor keduanya ke VPM → rekomendasi GO/NO-GO tag `v0.1.0` ke Developer.
