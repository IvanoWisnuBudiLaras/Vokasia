# VOK-H5-E3 — Integration tests jalur kritis (Testcontainers)

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-3 `backend/tests` | `h5-eng3-integration-tests` | GPT-5.4 Thinking | **max** | M4 | PRD NFR-MNT-03, NFR-SEC-03/04 |

## Tugas

Suite integration test jalur kritis end-to-end terhadap stack nyata (Testcontainers: PostgreSQL + RabbitMQ + Redis + MinIO). Ini jaring pengaman regresi untuk H6–H7 dan gate rilis. Bug yang ditemukan = temuan (lapor), BUKAN alasan melonggarkan test.

## Implementasi — `Vokasia.Tests/Integration/`

### 0. Fondasi
- `VokasiaApiFactory : WebApplicationFactory<Program>` — tujuan: boot API+Worker terhadap containers; helper `LoginAs(role, tenant) → HttpClient berotentikasi`; `SeedMinimal()` deterministik per test-class (bukan seed 900 siswa — cepat).

### 1. Suites (8 kelas, tiap test menyebut FR/NFR yang dibuktikan)
- `AuthFlowTests` — tujuan: code+PKCE → BFF exchange → panggil API ber-Bearer; token expired → refresh → sukses; NFR-SEC-01.
- `TenantIsolationTests` — tujuan: 6 endpoint utama (journals, placements, students, assessment, dashboard, audit) diakses lintas tenant → 404/403 semua; SuperAdmin tanpa acting-tenant → kosong; NFR-SEC-04.
- `RbacMatrixTests` — tujuan: sampel sistematis matrix 2.3 (≥15 kombinasi role×resource yang HARUS ditolak, mis. Teacher finalize → 403, Student approve → 403, Mentor lihat siswa placement lain → 404); NFR-SEC-03.
- `JournalLoopTests` — tujuan: submit → outbox → consumer → `StudentDailyStatus` Green → mentor approve → notif siswa + proyeksi portofolio; verifikasi async via polling helper (bukan sleep buta).
- `ImmutabilityTests` — tujuan: approve → mutasi 3 role → 409; finalize → revisi skor → 409; NFR-SEC-08.
- `AssessmentFinalizeTests` — tujuan: isi 2 sisi → finalize → `FinalScore` presisi vs 3 kasus hitung manual; belum lengkap → 422.
- `CertificateFlowTests` — tujuan: finalize → cron enqueue (dipicu manual) → PDF ada di MinIO → `VerifyCertificate` 200 tanpa field sensitif (assert properti DTO) → code palsu 404.
- `MagicLinkLifecycleTests` — tujuan: invite → exchange → sesi mentor scoped placement; reuse/expired → tolak; FR-AUTH-03.

### 2. Infrastruktur test
- Kategori `[Trait("Category","Integration")]` — tujuan: `dotnet test --filter Category=Integration` terpisah dari unit (cepat vs lengkap).
- `PollUntil(assert, timeout 10s, interval 200ms)` — tujuan: tunggu efek async deterministik.
- Runtime target: seluruh suite <8 mnt di mesin dev.

## Acceptance Criteria

- Given clean machine + Docker, When `dotnet test --filter Category=Integration`, Then hijau semua, reproducible 2× berturut.
- Tiap test memetakan ≥1 FR/NFR (komentar di test) — coverage jalur kritis terdokumentasi.
- Bug nyata yang tertangkap selama penulisan → dilaporkan sebagai daftar temuan ke VPM (bagian output).

## DoD + verifikasi runner (max)

Suite 2× dari containers baru → laporkan durasi & flaky (0 toleransi — flaky = perbaiki) → daftar temuan bug (jika ada) → PROMPT D → setor.
