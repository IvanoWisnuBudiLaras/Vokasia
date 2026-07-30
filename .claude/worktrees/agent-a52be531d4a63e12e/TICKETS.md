# TICKETS.md — 21 Ticket Sprint MVP Vokasia (H1–H7)

Dikeluarkan penuh 20 Jul 2026 (keputusan D10). Ticket H4–H7 = **provisional**: VPM merevisi saat checkpoint bila ada slip/temuan. 1 ticket = 1 ENG × 1 hari. Branch: `h{N}-eng{X}-{slug}`.

> **UPDATE D11 (21 Jul)**: eksekusi harian memakai **folder `ticket/`** — `ticket/template.md` (prompt + effort runner) + 21 file `VOK-*.md` (fungsi+parameter+tujuan). Coder = ChatGPT, runner = Sonnet 5 + Windows-MCP. File ini tetap jadi ringkasan AC/gate; bila beda detail, `ticket/*.md` menang.

**Model mapping**: fase chat (diskusi desain sebelum koding) memakai model di kolom ticket — `Thinking` = GPT-5.4 Thinking (atau 5.6 Sol), `Terra` = GPT-5.6 Terra, `Luna` = GPT-5.6 Luna. **Semua implementasi di repo = Codex (GPT-5.3-Codex).** Cek nama persis di model picker akunmu.

## Cara pakai (ritual pagi)

1. Salin **PROMPT TEMPLATE** di bawah, isi placeholder `{...}` dari blok ticket hari itu.
2. Paste ke chat ENG ybs (model sesuai kolom ticket) → diskusi singkat → lanjut Codex untuk implementasi.
3. Sore: hasil (kode+test+walkthrough) → review VPM → fix Blocker/Critical → Dev merge.

## PROMPT TEMPLATE (paste per ticket)

```
Kamu adalah {ENG-x} proyek Vokasia. Baca lampiran: SOUL.md (role-mu), AGENTS.md
(aturan teknis wajib), potongan PRD §{ref}, kontrak OpenAPI terkait{, DESIGN.md
khusus ENG-2}. Kerjakan TICKET di bawah, di branch {branch}.

KONTRAK OUTPUT — wajib, urut. Kiriman tanpa salah satu ini DITOLAK review:
1. TASK LIST  — checklist subtask, tulis SEBELUM menulis kode
2. IMPLEMENTASI — kode + test menyertai (unit utk logic, integration utk endpoint kritis)
3. WALKTHROUGH — per file: apa yang berubah, kenapa, kaitannya ke AC mana
4. OUTPUT TEST verbatim (dotnet test / bun test) — bukan klaim "sudah ditest"
5. [ASSUMPTION] — daftar asumsi yang kamu ambil, atau tulis "tidak ada"

Larangan: koding sebelum TASK LIST · selesai tanpa WALKTHROUGH · menyentuh file
di luar wilayahmu · mengubah kontrak OpenAPI/skema DB · menambah dependency baru ·
skip/hapus test. Ide di luar ticket → tulis sebagai catatan, JANGAN implement.
Pertanyaan blocking maks 3 → ke Developer.

--- TICKET ---
{paste blok ticket dari file ini}
```

## Matrix 21 ticket

| Hari | ENG-1 (backend) | ENG-2 (frontend) | ENG-3 (auth/QA) | Gate sore |
|---|---|---|---|---|
| H1 | E1 Compose+migrations | E2 Tokens+shells | E3 OpenIddict+threat model | **M0** freeze kontrak |
| H2 | E1 Seeder+endpoint inti | E2 Login UI+guards | E3 BFF+RBAC+magic link | **M1** login 4 role |
| H3 | E1 Journal API+cron | E2 UI jurnal+approve | E3 Immutability+validasi | **M2** core loop |
| H4 | E1 Outbox+consumers | E2 Dashboard RAG | E3 Idempotency+email | **M3** early warning |
| H5 | E1 Assessment+sertifikat | E2 UI nilai+kunjungan | E3 Integration tests | **M4** PDF QR |
| H6 | E1 /sa+billing+portfolio | E2 UI SA+portofolio publik | E3 Impersonation+hardening | **M5** platform ops |
| H7 | E1 Perf+ops+README | E2 States+low-data | E3 E2E+security report | **M6** v0.1.0 |

---

# HARI 1 — Fondasi (Gate M0: kontrak beku)

> Gate M0 (bukan ticket ENG): sore H1, VPM+Dev me-review & membekukan OpenAPI + skema DB. Setelah beku, deviasi = temuan review.

### VOK-H1-E1 · Docker compose 7 service + migration EF seluruh entitas
`ENG-1` · Chat: **Terra** → Codex · Branch `h1-eng1-compose-migrations` · PRD §0, §2.4, Bagian 2
- **Fungsi/unit**: docker-compose.yml 7 service (api, worker, frontend, postgres, redis, rabbitmq, minio) + healthcheck; entitas + konfigurasi EF: `Tenant, AppUser, Company(global), Period, HolidayCalendar, Student, Placement, JournalSlot, JournalEntry, JournalPhoto, Competency, Visit, RubricTemplate, Assessment, Certificate, Portfolio, Plan, FeatureFlag, Invoice, Notification, AuditLog, OutboxMessage, ProcessedMessage`; global query filter `tenant_id` (stub, diaktifkan penuh H2-E3); migration `Initial`; index dasar query dashboard.
- **User story**: Sebagai Developer, saya butuh infra & skema DB lengkap agar semua modul H2+ dibangun di atas kontrak beku (M0).
- **AC**:
  - Given clean state, When `docker compose up -d`, Then 7 service healthy.
  - Given DB kosong, When `dotnet ef database update`, Then semua tabel+FK+index terbentuk; dijalankan ulang → tanpa error.
  - Given entitas tenant-scoped, When query tanpa tenant context, Then global filter memblokir (1 test membuktikan).
- **DoD**: build+test hijau · compose up dari clean state terbukti · skema lolos review VPM (gate M0).

### VOK-H1-E2 · Design tokens + 5 shell route group + komponen inti
`ENG-2` · Chat: **Terra** → Codex · Branch `h1-eng2-tokens-shells` · PRD §4.1–4.3 + DESIGN.md
- **Fungsi/unit**: tokens dari DESIGN.md → Tailwind config/CSS vars; route groups `(sa) (school) (mentor) (student) p/[slug] verify/[code]` dengan layout shell; komponen inti 7: `Button, Input, Card, StatusBadge(RAG), EmptyState, ErrorState, OfflineBanner`; halaman landing ringkas + tombol login.
- **User story**: Sebagai tim, kami butuh fondasi visual konsisten agar semua UI H2+ memakai komponen sama tanpa hardcode style.
- **AC**:
  - Given repo clean, When `bun run build`, Then sukses tanpa error/type error.
  - Given 5 segment dibuka, When belum ada data, Then shell tampil dengan EmptyState (bukan layar kosong/buntu).
  - Given komponen inti, When dipakai di W1-style page contoh, Then hanya memakai tokens (tanpa warna/spacing hardcode) & target sentuh ≥44px.
- **DoD**: build hijau · tanpa hardcode di luar tokens · review visual VPM vs DESIGN.md.

### VOK-H1-E3 · OpenIddict + Identity + threat model
`ENG-3` · Chat: **Thinking** → Codex · Branch `h1-eng3-openiddict` · PRD §2.1 AUTH, §2.4 auth flow
- **Fungsi/unit**: OpenIddict server di Vokasia.Api (Authorization Code + PKCE wajib); ASP.NET Identity (user store per tenant + SuperAdmin global); konfigurasi JWT access 15 mnt + refresh; seed OAuth client BFF; skeleton endpoint `Authorize`, `ExchangeToken`; threat model 1 halaman (aset, aktor, permukaan serangan, mitigasi → jadi acuan review security-ku).
- **User story**: Sebagai platform, saya butuh OAuth server internal yang benar sejak awal agar auth tidak di-retrofit.
- **AC**:
  - Given client BFF terdaftar, When authorization code flow + PKCE dijalankan, Then access token (15 mnt) + refresh token terbit.
  - Given flow tanpa PKCE, When authorize, Then ditolak.
  - Given token terbit, When inspect, Then claims memuat `sub, tenant_id, role`.
- **DoD**: test flow hijau · threat model diserahkan · review VPM.

---

# HARI 2 — Auth vertikal & data (Gate M1: login 4 role → dashboard berisi seed)

### VOK-H2-E1 · Seeder demo + endpoint periods/companies/placements
`ENG-1` · Chat: **Terra** → Codex · Branch `h2-eng1-seeder-core-endpoints` · PRD FR-TEN-*, FR-X-04
- **Fungsi (±17)**: Seeder: `SeedWilayahNpsn` (API emsifa + NPSN), `SeedDemoData` (3 tenant, 100 DUDI, 900 siswa, 90 hari jurnal, skenario ghosting & rejected — via Bogus, idempoten). Endpoints: `CreatePeriod, UpdatePeriod, ListPeriods, SetHolidayCalendar, ImportStudentsCsv, GetImportResult, CreateStudent, UpdateStudent, ListStudents, LinkCompanyToTenant, ProposeCompany, SetCompanySlots, CreatePlacement, BulkCreatePlacements, AssignTeacher, ListPlacements, GetPlacement`.
- **User story**: Sebagai TenantAdmin, saya bisa menyiapkan periode, siswa, DUDI, dan placement agar siklus PKL bisa dimulai; sebagai tim, kami punya data demo realistis.
- **AC**:
  - Given clean DB, When 1 perintah seed, Then 3 tenant + 900 siswa + 90 hari jurnal terisi < 5 mnt; dijalankan ulang → tidak duplikat.
  - Given CSV dengan 2 baris rusak, When import, Then baris valid masuk, error per baris dilaporkan (FR-TEN-02).
  - Given placement dibuat, When event `PlacementCreated`, Then tercatat di outbox (consumer menyusul H4).
- **DoD**: test endpoint kritis hijau · seed terbukti dari clean state · sesuai OpenAPI beku.

### VOK-H2-E2 · Login UI + route guards
`ENG-2` · Chat: **Luna** → Codex · Branch `h2-eng2-login-guards` · PRD FR-AUTH-01/05, §4.2
- **Fungsi/unit (±6)**: halaman login (redirect ke BFF flow); `proxy.ts` middleware guard per segment × role; session state client (dari cookie session BFF); dashboard shell 4 role menampilkan data seed (nama, tenant, ringkasan); logout UI; error/loading states.
- **AC**:
  - Given user role Student, When akses `/app`, Then redirect/403 — dan sebaliknya untuk 4 role × 5 segment (matrix test).
  - Given login sukses, When buka dashboard role-nya, Then data seed tampil (bukti wiring end-to-end).
  - Given browser storage diinspeksi, Then **tidak ada token** di localStorage/sessionStorage — hanya httpOnly cookie.
- **DoD**: guard matrix teruji · build hijau · demo login 4 role jalan (gate M1 bersama E3).

### VOK-H2-E3 · BFF token exchange + RBAC + tenant filter + magic link
`ENG-3` · Chat: **Thinking** → Codex · Branch `h2-eng3-bff-rbac-magiclink` · PRD FR-AUTH-01..07, §2.3
- **Fungsi (±16)**: BFF route handlers: `handleLogin, handleCallback, handleSession, handleLogout, proxyWithBearer, refreshOnExpiry` (JWT+refresh di Redis; rotation + reuse detection; revocation instan). API: `RegisterRbacPolicies` (matrix 2.3), `TenantResolutionMiddleware`, aktivasi penuh global query filter, `PlacementScopeHandler` (mentor per placement), `WriteAuditLog` service. Magic link: `CreateMentorInvite, SendMagicLink, ValidateMagicToken (sekali pakai, TTL 72 jam), ExchangeMagicToken`.
  - Prioritas bila terdesak: BFF → RBAC/filter → magic link (magic link boleh geser pagi H3 — lapor checkpoint, jangan diam).
- **AC**:
  - Given refresh token dipakai 2×, When exchange kedua, Then seluruh session keluarga token dicabut (reuse detection).
  - Given user tenant A, When query data tenant B (langsung ke API), Then 404/403 — test isolasi lintas endpoint.
  - Given mentor magic link, When dipakai 2× atau > 72 jam, Then ditolak.
  - Given logout/nonaktif, When pakai session lama, Then ditolak instan (Redis revoked).
- **DoD**: test isolasi tenant + RBAC matrix + reuse detection hijau · audit log tercatat untuk aksi auth sensitif.

---

# HARI 3 — Core loop jurnal (Gate M2: siswa isi → mentor approve via magic link di HP)

### VOK-H3-E1 · Journal endpoints + presigned upload + cron slot/reminder
`ENG-1` · Chat: **Terra** → Codex · Branch `h3-eng1-journal-api-cron` · PRD FR-JRN-01..06
- **Fungsi (±14)**: `GetTodayJournal, SubmitJournal, GetPresignedUploadUrl (MinIO), AttachPhoto, ListJournals, ApproveJournal, RejectJournal, BatchApprove, AddTeacherComment, GetPendingApprovals, ListCompetencies`; cron Hangfire (WIB eksplisit): `GenerateDailyJournalSlots` (05:00, per placement aktif, skip libur), `RemindEmptyJournals` (19:00); event `JournalSubmitted/Approved` → outbox.
- **AC**:
  - Given placement aktif + hari kerja, When cron 05:00 WIB jalan, Then slot ter-generate; hari libur kalender → skip.
  - Given jurnal >500 karakter atau foto ke-4, When submit, Then ditolak validasi.
  - Given 10 jurnal pending, When `BatchApprove`, Then semua Approved + event terbit per jurnal.
  - Given upload, When minta URL, Then presigned MinIO (bukan upload lewat API body).
- **DoD**: test endpoint + cron (time-mocked) hijau · sesuai OpenAPI · tanpa N+1 di `ListJournals`.

### VOK-H3-E2 · UI siswa isi jurnal (W1) + mentor batch approve (W2)
`ENG-2` · Chat: **Terra** → Codex · Branch `h3-eng2-journal-ui` · PRD §4.3 W1–W2, NFR-UX
- **Fungsi/unit (±8)**: `/student` TodayPage sesuai W1 (JournalForm ≤500 kar + counter, pilih kompetensi, PhotoUploader 0/3 via presigned, tombol KIRIM besar, streak mingguan); HistoryPage; `/mentor` PendingListPage sesuai W2 (pilih semua, BatchApproveBar, expand detail, RejectDialog + alasan); semua state loading/empty/error/offline.
- **AC**:
  - Given siswa di HP 360px/3G, When isi jurnal lengkap, Then selesai ≤2 menit (alur ≤3 tap + ketik).
  - Given mentor 8 jurnal pending, When pilih semua → approve, Then batch sukses ≤2 menit, optimistic update.
  - Given koneksi putus, When buka halaman, Then OfflineBanner tampil, tanpa layar buntu.
- **DoD**: build hijau · layout match W1/W2 · hanya komponen inti+tokens · demo M2 di HP jalan.

### VOK-H3-E3 · Immutability + validasi menyeluruh + rate limit
`ENG-3` · Chat: **Thinking** → Codex · Branch `h3-eng3-immutability-validation` · PRD FR-JRN-04, NFR-SEC-06/08
- **Fungsi (±8)**: `EnsureJournalMutable` (domain guard: Approved = immutable, error eksplisit); FluentValidation seluruh request M3–M4 scope (journal, period, placement, import); rate limiter (login 5/mnt, endpoint publik 10/mnt); sanitasi input teks; test suite: immutability (update/delete pasca-approve ditolak), validasi boundary, rate limit 429.
- **AC**:
  - Given jurnal Approved, When siswa/mentor/admin coba ubah via API, Then 409/403 + pesan jelas — tanpa jalur unlock (unlock ber-audit = fase 2).
  - Given 6 percobaan login/menit, When ke-6, Then 429.
  - Given payload berbahaya (script/HTML), When submit, Then tersanitasi/ditolak.
- **DoD**: seluruh test hijau · tidak ada endpoint scope H1–H3 tanpa validator.

---

# HARI 4 — Async & early warning (Gate M3: 3 hari kosong → merah + email guru < 1 mnt)

### VOK-H4-E1 · MassTransit + outbox + consumers + cron ghosting
`ENG-1` · Chat: **Thinking** → Codex · Branch `h4-eng1-outbox-consumers` · PRD FR-X-02, FR-JRN-03/07, §2.4
- **Fungsi (±12)**: `SaveToOutboxInterceptor` (EF SaveChanges), `OutboxDispatcher` (background publish ke RabbitMQ), `EnsureNotProcessed` (idempotency store); consumers di Worker: `JournalSubmittedConsumer` (status projector RAG), `StreakCounterConsumer`, `PhotoUploadedConsumer` (compress + strip EXIF-GPS + thumbnail), `JournalApprovedConsumer` (notify + portfolio projector), `MentorInviteSenderConsumer`; cron `FlagGhostingStudents` (21:00 WIB: ≥3 hari kerja kosong → status MERAH + notif guru & admin); `CreateNotification`, `ListMyNotifications`, `MarkRead`.
- **AC**:
  - Given RabbitMQ down, When jurnal disubmit, Then event tersimpan di outbox dan terkirim setelah broker hidup (tidak hilang).
  - Given consumer menerima message sama 2×, When proses, Then efek hanya 1× (idempoten, dibuktikan test).
  - Given siswa 3 hari kerja tanpa jurnal, When cron 21:00, Then status MERAH + notifikasi guru & admin terkirim < 1 mnt.
  - Given foto ber-EXIF-GPS, When diproses, Then hasil tanpa GPS metadata + thumbnail terbentuk.
- **DoD**: retry policy + DLQ terpasang · test outbox/idempotency hijau · demo M3 jalan.

### VOK-H4-E2 · Dashboard admin RAG (W3) + halaman guru bimbingan
`ENG-2` · Chat: **Terra** → Codex · Branch `h4-eng2-dashboard-rag` · PRD §4.3 W3
- **Fungsi/unit (±6)**: `/app` DashboardPage sesuai W3 (4 kartu KPI: jurnal hari ini %, approval pending, kunjungan terlambat, flagged; daftar SISWA BERMASALAAH 🔴🟡 + link detail); halaman guru: daftar siswa bimbingan + status RAG + riwayat jurnal + komentar; notifikasi in-app (bell + list + mark read); filter per periode.
- **AC**:
  - Given seed dengan skenario ghosting, When buka dashboard, Then siswa MERAH tampil di atas dengan alasan ("4 hr kosong").
  - Given guru login, When buka bimbingan, Then hanya siswa yang di-assign kepadanya (scope teruji).
  - Given 900 siswa seed, When load dashboard, Then p95 < 300ms (query agregat, bukan loop).
- **DoD**: build hijau · match W3 · scope guru benar · tanpa N+1.

### VOK-H4-E3 · Idempotency & DLQ tests + template email
`ENG-3` · Chat: **Thinking** → Codex · Branch `h4-eng3-dlq-email` · PRD FR-X-02/03
- **Fungsi (±7)**: test suite async: duplicate delivery, out-of-order, poison message → DLQ setelah retry policy, replay manual dari DLQ; `SendEmail` infra (SMTP/Resend) + template seragam (base layout + 5 template: undangan mentor, reminder jurnal, ghosting alert, export siap, invoice); konfigurasi retry/backoff MassTransit; dokumentasi singkat cara replay DLQ.
- **AC**:
  - Given consumer melempar exception permanen, When retry habis, Then message masuk DLQ + terlihat di health (H6).
  - Given template email, When render 5 jenis, Then konsisten (header/footer sama), plain-text fallback ada.
  - Given email gagal kirim, When retry, Then tidak menduplikasi notifikasi in-app.
- **DoD**: test async hijau · 5 template terkirim teruji ke mailbox dev.

---

# HARI 5 — Penilaian & sertifikat (Gate M4: finalize → PDF ber-QR via worker)

### VOK-H5-E1 · Visits + rubrik + assessment + export + certificate worker
`ENG-1` · Chat: **Terra** → Codex · Branch `h5-eng1-assessment-certificate` · PRD FR-ASM-*, FR-CRT-01/02
- **Fungsi (±17)**: `CreateVisit, ListVisits`; `CreateRubricTemplate (seed default Kurikulum Merdeka: teknis/softskill/kehadiran + bobot), UpdateRubric, GetRubric`; `OpenAssessmentPhase` (cron H-14 + reminder), `SubmitMentorScores` (aspek industri), `SubmitTeacherScores` (aspek sekolah), `ComputeWeightedScore`, `FinalizeAssessment` (TenantAdmin, lock), `GetAssessment`; `GetGradeRecap`, `RequestExport` (202 + job), `ExportRequestedConsumer` (Excel/PDF), `NotifyExportReady`; `EnqueueCertificateBatch` (cron H+1 finalisasi), `CertificateGeneratorConsumer` (QuestPDF: identitas, DUDI, durasi, nilai, QR verifikasi), `GetCertificate`, `VerifyCertificate` (publik, tanpa data sensitif).
- **AC**:
  - Given mentor & guru mengisi skor, When finalize oleh TenantAdmin, Then skor berbobot terhitung benar (test kalkulasi) dan nilai terkunci — edit pasca-finalize ditolak.
  - Given finalisasi periode, When cron H+1, Then batch sertifikat masuk queue; 500 sertifikat < 10 mnt; API tetap responsif.
  - Given certCode valid, When `/verify` publik, Then identitas minimal tampil (tanpa NISN/kontak); code palsu → not found.
  - Given export diminta, When selesai, Then notifikasi + file downloadable (202 pattern, bukan blocking).
- **DoD**: test kalkulasi + lock + verify hijau · PDF sample di-review VPM · demo M4 jalan.

### VOK-H5-E2 · UI kunjungan (W4) + rubrik + rekap nilai
`ENG-2` · Chat: **Terra** → Codex · Branch `h5-eng2-assessment-ui` · PRD §4.3 W4
- **Fungsi/unit (±7)**: guru mobile VisitFormPage sesuai W4 (tanggal otomatis, catatan, foto, ttd canvas sederhana) + riwayat per placement; `/mentor` ScoreFormPage (rubrik aspek industri); `/app` rubrik editor (aspek+bobot), RekapNilaiPage (tabel skor per siswa + status finalisasi) + tombol Finalize (konfirmasi) + RequestExport 202 → toast + notifikasi siap.
- **AC**:
  - Given guru di HP, When isi kunjungan + ttd, Then tersimpan & muncul di riwayat placement.
  - Given mentor isi skor, When simpan draft lalu submit, Then guru & admin melihat status pengisian.
  - Given admin finalize, When sukses, Then UI mengunci form skor (read-only) + badge "final".
- **DoD**: build hijau · match W4 · state lengkap · export flow 202 teruji manual.

### VOK-H5-E3 · Integration tests jalur kritis
`ENG-3` · Chat: **Thinking** → Codex · Branch `h5-eng3-integration-tests` · PRD NFR-MNT-03
- **Fungsi (±8 suite, Testcontainers)**: auth flow penuh (code+PKCE→BFF→API); isolasi tenant lintas 6 endpoint utama; RBAC matrix 2.3 (approve tanpa role mentor → 403, dst); core loop jurnal end-to-end (submit→outbox→consumer→status); immutability pasca-approve; assessment finalize + lock; certificate generate→verify; magic link lifecycle.
- **AC**:
  - Given clean containers, When `dotnet test --filter Integration`, Then semua hijau, reproducible, tanpa dependensi state lokal.
  - Given test menemukan bug, Then bug dilaporkan sebagai temuan (bukan test dilonggarkan agar lulus).
- **DoD**: suite jalan di clean state · laporan coverage jalur kritis singkat ke VPM.

---

# HARI 6 — Superadmin, portofolio, billing (Gate M5: provisioning tenant + portofolio publik)

### VOK-H6-E1 · Endpoint /sa + billing + portfolio backend
`ENG-1` · Chat: **Terra** → Codex · Branch `h6-eng1-sa-billing-portfolio` · PRD FR-SA-*, FR-BIL-*, FR-CRT-03
- **Fungsi (±26)**: Tenants: `CreateTenant (wizard + seed rubrik default), UpdateTenant, ListTenants, GetTenant, DeactivateTenant`; DUDI global: `CreateCompany, VerifyCompany, MergeCompanies (ber-riwayat), ListCompanies, SearchCompanies`; Plans: `CreatePlan, UpdatePlan, SetFeatureFlag, OverrideTenantFlag, GetEffectiveFlags`; Ops: `GetPlatformKpis, GetSystemHealth (queue depth, DLQ, jobs, error rate), QueryAuditLogs`; Billing: `GenerateMonthlyInvoices (cron tgl 1 02:00 + email), GetInvoices, UploadPaymentProof, ConfirmPayment, CheckQuotaOnPlacement (lewat kuota → blokir placement baru, data tidak dikunci)`; Portfolio: `GetMyPortfolio, UpdatePortfolio, PublishPortfolio (opt-in; validasi tanpa kontak/NISN), UnpublishPortfolio, GetPublicPortfolio (/p/{slug}, cache-able)`.
  - Prioritas: portfolio publik + tenant provisioning (gate M5) → billing → KPI/health.
- **AC**:
  - Given wizard provisioning, When tenant baru dibuat, Then rubrik default + admin pertama ter-seed, siap login.
  - Given tenant lewat kuota plan, When buat placement baru, Then diblokir dengan pesan; data lama tetap bisa dibaca (FR-BIL-03).
  - Given merge 2 DUDI duplikat, When merge, Then relasi placement pindah + riwayat merge tercatat.
  - Given portofolio di-publish, When GET publik, Then tanpa NISN/kontak; unpublish → 404.
- **DoD**: test endpoint kritis hijau · cron invoice teruji (time-mocked) · sesuai OpenAPI.

### VOK-H6-E2 · UI Superadmin (W5) + portofolio publik (W6) + verify
`ENG-2` · Chat: **Terra** → Codex · Branch `h6-eng2-sa-portfolio-ui` · PRD §4.3 W5–W6, NFR-PERF-01
- **Fungsi/unit (±10)**: `/sa` sesuai W5: KPI cards, TenantsPage + wizard, DudiRegistryPage (verifikasi + merge UI), PlansPage, InvoicesPage (konfirmasi bukti transfer), HealthPage (queue/DLQ/jobs), AuditPage (filter aktor/entitas/tanggal); `/student` PortfolioEditor (pilih sampel approved, toggle publish + consent copy); publik `/p/[slug]` sesuai W6 (SSG/PPR + cache, LCP < 2,5 dtk 3G); `/verify/[code]` (hasil valid/tidak + data minimal); `/app` BillingPage (invoice + upload bukti).
- **AC**:
  - Given siswa publish portofolio, When buka `/p/{slug}` incognito, Then tampil kompetensi + sampel + sertifikat, tanpa kontak/NISN.
  - Given certCode dari PDF, When buka `/verify/{code}`, Then status terverifikasi tampil.
  - Given Lighthouse mobile 3G di `/p/{slug}`, Then LCP < 2,5 dtk.
- **DoD**: build hijau · match W5/W6 · demo M5 (provisioning → login admin baru → portofolio publik) jalan.

### VOK-H6-E3 · Impersonation ber-audit + hardening + scan
`ENG-3` · Chat: **Thinking** → Codex · Branch `h6-eng3-impersonation-hardening` · PRD FR-AUTH-07, NFR-SEC-07
- **Fungsi (±8)**: `StartImpersonation` (SuperAdmin → user target; session ditandai; banner UI), `EndImpersonation`; semua aksi saat impersonasi → AuditLog dengan aktor asli; sweep secrets (tidak ada hardcode; semua via env; `.env` tidak ter-commit); dependency scan (`dotnet list package --vulnerable`, `bun audit` / osv-scanner) + container scan (trivy) → laporan; security headers (CSP dasar, HSTS, nosniff); verifikasi rate limit publik `/p` `/verify` 10/mnt.
- **AC**:
  - Given SuperAdmin impersonate TenantAdmin, When melakukan aksi, Then audit log mencatat `actor=SA, as=user`; UI banner "sedang impersonasi".
  - Given scan dijalankan, Then temuan High/Critical = 0 atau punya justifikasi tertulis ke Developer.
  - Given response publik, Then security headers terpasang (dibuktikan test).
- **DoD**: test impersonation+audit hijau · laporan scan diserahkan · temuan H/C dibereskan atau di-waive Dev.

---

# HARI 7 — Hardening & rilis (Gate M6: v0.1.0 — demo penuh clean state, test hijau, security lolos)

### VOK-H7-E1 · Perf pass + health + backup + README deploy
`ENG-1` · Chat: **Terra** → Codex · Branch `h7-eng1-perf-ops` · PRD NFR-PERF, NFR-REL
- **Fungsi (±8)**: audit query dashboard (index tambahan bila perlu, hapus N+1 tersisa); verifikasi p95 < 300ms (k6/bombardier singkat pada 5 endpoint tersibuk + burst 50 req/dtk submit jurnal); `MapHealthChecks` semua service + endpoint `/health` compose; backup script `pg_dump` harian (cron host) + retensi 14 hari + **uji restore**; README: runbook deploy VPS 1 mesin, urutan up, restore, troubleshooting.
- **AC**:
  - Given burst 50 req/dtk submit jurnal 5 mnt, Then error rate 0, queue menyerap (NFR-PERF-03).
  - Given backup semalam, When restore ke DB kosong, Then aplikasi hidup dengan data utuh (dibuktikan).
  - Given `docker compose up` di clean state + 1 perintah seed, Then seluruh app hidup (NFR-MNT-04).
- **DoD**: angka perf dilampirkan · restore terbukti · README lengkap.

### VOK-H7-E2 · States lengkap + low-data + Lighthouse + PWA
`ENG-2` · Chat: **Luna** → Codex · Branch `h7-eng2-states-lowdata` · PRD NFR-UX-04, NFR-PERF-05
- **Fungsi/unit (±7)**: sweep semua layar: loading/empty/error/offline lengkap (checklist per halaman); initial payload `/student` < 200KB (analisa bundle, dynamic import, hapus dependency berat); PWA manifest + ikon + installability `/student`; Lighthouse pass mobile (perf & a11y) halaman kunci; target sentuh ≥44px audit; copy bahasa sederhana konsisten.
- **AC**:
  - Given jaringan diputus di tiap layar utama, Then state offline tampil, tanpa crash/layar buntu.
  - Given bundle analyzer, Then `/student` initial < 200KB terbukti (screenshot dilampirkan).
  - Given Lighthouse mobile `/student` & `/p/{slug}`, Then perf ≥ 85, a11y ≥ 90.
- **DoD**: checklist states per halaman diserahkan · angka bundle & Lighthouse dilampirkan.

### VOK-H7-E3 · E2E Playwright 5 persona + laporan security
`ENG-3` · Chat: **Thinking** → Codex · Branch `h7-eng3-e2e-security` · PRD NFR-MNT-03, NFR-SEC-*
- **Fungsi (±7)**: E2E Playwright 5 persona: SuperAdmin (provisioning→konfirmasi invoice), TenantAdmin (periode→placement→finalize), Teacher (monitoring→kunjungan→komentar), Mentor (magic link→batch approve→nilai), Student (login→jurnal+foto→portofolio publish); jalan headless dari clean state + seed; laporan security final: checklist NFR-SEC-01..08 dengan bukti per butir (test/screenshot/config) → input gate M6.
- **AC**:
  - Given clean state + seed, When E2E suite jalan, Then 5 persona lulus end-to-end tanpa intervensi manual.
  - Given laporan security, Then setiap NFR-SEC punya status lolos/gagal + bukti; gagal = blocker rilis.
- **DoD**: E2E hijau di clean state · laporan security diserahkan → keputusan tag `v0.1.0` oleh Dev.

---

## Catatan pengendalian

- Ticket H4–H7 provisional — revisi oleh VPM saat checkpoint sore; perubahan dicatat di DECISIONS.md.
- Fitur Should/Could (presensi, offline queue PWA, digest ortu, reset password, broadcast) **tidak ada di ticket manapun** — backlog Minggu 2+.
- Kiriman ENG tanpa TASK LIST / WALKTHROUGH / output test = otomatis REQUEST CHANGES.
