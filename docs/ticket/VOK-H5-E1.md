# VOK-H5-E1 — Visits + rubrik + assessment + export async + certificate worker

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-1 `backend/` | `h5-eng1-assessment-certificate` | GPT-5.4 Thinking | **extra high** | **M4** (PDF ber-QR) | PRD FR-ASM-01..06, FR-CRT-01/02 |

## Tugas

Rantai penilaian lengkap: kunjungan guru → rubrik → skor dua sisi → skor berbobot → finalisasi terkunci → sertifikat PDF ber-QR via worker → verifikasi publik. Plus export rekap async.

## Implementasi

### 1. Kunjungan — policy `Teacher+`
- `CreateVisit(Guid placementId, CreateVisitRequest{Date, Notes, PhotoKey?, SignatureDataUrl?}) → VisitDto` — tujuan: catat monitoring (W4); signature dataURL → simpan PNG MinIO `SignatureKey`; audit.
- `ListVisits(Guid placementId) → List<VisitDto>` — tujuan: riwayat per placement (FR-ASM-01); admin melihat kunjungan terlambat via dashboard (sudah dihitung `LateVisits`).

### 2. Rubrik — policy `TenantAdmin`
- `CreateRubricTemplate(CreateRubricRequest{Name, Aspects[{Name, Kind: Teknis|Softskill|Kehadiran, Weight}]}) → RubricDto` — tujuan: template; validasi ΣWeight=100; seed default Kurikulum Merdeka sudah dari provisioning (H6) — untuk tenant seed, pastikan ada.
- `UpdateRubric(Guid id, ...)` — tujuan: ubah selama belum dipakai assessment final; sesudah → 409.
- `GetRubric(Guid periodId) → RubricDto` — tujuan: rubrik aktif periode (dipakai form skor mentor/guru).

### 3. Assessment
- `OpenAssessmentPhase()` — cron harian 06:00 WIB. Tujuan: periode dengan `EndDate - 14 == today` → `Status=Assessment` + notif mentor & guru "fase penilaian dibuka" (FR-ASM-05); idempoten.
- `SubmitMentorScores(Guid placementId, List<ScoreInput{AspectId, Value 0..100}>)` — policy `MentorOwnPlacement`. Tujuan: skor aspek industri (Teknis+Kehadiran sisi DUDI); draft boleh direvisi sampai final.
- `SubmitTeacherScores(Guid placementId, List<ScoreInput>)` — policy `Teacher+`. Tujuan: skor aspek sekolah (Softskill dsb sesuai rubrik).
- `ComputeWeightedScore(Guid placementId) → decimal` — **pure function, unit-tested**. Tujuan: Σ(nilai aspek × bobot)/100 dengan pembagian sisi mentor/guru sesuai `Kind`; pembulatan 2 desimal `MidpointRounding.AwayFromZero`; aspek belum diisi → exception eksplisit (bukan 0 diam-diam).
- `FinalizeAssessment(Guid periodId, Guid? placementId)` — policy `TenantAdmin`. Tujuan: validasi semua skor lengkap → hitung `FinalScore`, `IsFinal=true` (immutable — guard H3-E3) + publish `AssessmentFinalized` per placement + audit (FR-ASM-04).
- `GetAssessment(Guid placementId) → AssessmentDto{Aspects[], MentorDone, TeacherDone, FinalScore?, IsFinal}` — tujuan: satu DTO untuk semua layar skor.

### 4. Rekap & export
- `GetGradeRecap(Guid periodId) → List<RecapRow{Student, Company, MentorAvg, TeacherAvg, FinalScore, Status}>` — tujuan: tabel rekap (proyeksi, tanpa N+1).
- `RequestExport(Guid periodId, ExportFormat: Xlsx|Pdf) → 202 {ExportId}` — tujuan: antre `ExportRequested` (FR-ASM-06 pola 202).
- `ExportRequestedConsumer` — tujuan: bangun file (ClosedXML/QuestPDF) → MinIO → `CreateNotification(ExportReady{DownloadUrl presigned 24 jam})` + email.

### 5. Sertifikat
- `EnqueueCertificateBatch()` — cron 06:30 WIB. Tujuan: periode finalized H+1 → antre `CertificateRequested` per placement lulus; idempoten (skip yang sudah punya).
- `CertificateGeneratorConsumer` — tujuan: `GenerateCertificatePdf(placementId)`: QuestPDF berisi identitas siswa, sekolah, DUDI, durasi, nilai akhir + **QR ke `/verify/{certCode}`**; `CertCode =` random 12 kar url-safe (bukan sequential); simpan MinIO + row `Certificate`; throughput target 500 <10 mnt (NFR-PERF-04).
- `GetCertificate(Guid placementId) → {DownloadUrl}` — policy siswa sendiri/admin. Tujuan: unduh via presigned.
- `VerifyCertificate(string certCode) → VerifyDto{StudentName, SchoolName, CompanyName, PeriodLabel, IssuedAt, Valid}` — **publik + rate limit `public`**. Tujuan: verifikasi tanpa data sensitif (tanpa NISN/kontak/nilai — FR-CRT-02).

## Acceptance Criteria

- Given skor lengkap 2 sisi, When finalize, Then `FinalScore` = hitungan manual kasus uji (3 kasus di unit test) & terkunci; revisi skor → 409.
- Given skor belum lengkap, When finalize, Then 422 + daftar yang kurang.
- Given finalisasi kemarin, When cron, Then batch antre; 500 sertifikat <10 mnt; API tetap responsif saat generate.
- Given certCode valid/palsu, Then verify 200 minimal-data / 404.
- Given export 900 siswa, Then 202 instan + file+notif <2 mnt.

## DoD + verifikasi runner (extra high)

Unit `ComputeWeightedScore` dulu (test-first) → suite 2× → generate batch sertifikat di seed + ukur waktu → buka 1 PDF (lampirkan ke VPM) + scan QR → setor.
