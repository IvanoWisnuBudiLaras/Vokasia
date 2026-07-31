# VOK-H6-E1 — Endpoint /sa (tenants, DUDI, plans, ops) + billing + portfolio backend

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-1 `backend/` | `h6-eng1-sa-billing-portfolio` | GPT-5.3-Codex | high | **M5** | PRD FR-SA-01..07, FR-BIL-01..03, FR-CRT-03 |

## Tugas

Hari CRUD terbesar (±26 fungsi, mayoritas sederhana). **Prioritas urutan: portfolio publik + tenant provisioning (gate M5) → billing → KPI/health/audit.** Semua endpoint `/sa/*` policy `SaOnly`.

## Implementasi

### 1. Tenants
- `CreateTenant(CreateTenantRequest{SchoolName, Npsn?, City, AdminEmail, AdminName, PlanId}) → TenantDto` — tujuan: **wizard provisioning**: buat tenant + seed `RubricTemplate` default Kurikulum Merdeka + user TenantAdmin pertama + email undangan set password; satu transaksi; audit.
- `UpdateTenant(Guid id, UpdateTenantRequest)` · `ListTenants(TenantFilter{Search?, PlanId?, Active?, Page})` · `GetTenant(Guid id) → TenantDetailDto{Stats}` — tujuan: kelola & pantau tenant.
- `DeactivateTenant(Guid id, string reason)` — tujuan: nonaktif → semua session user tenant dicabut (hook H2-E3) + placement baru terblokir; data TIDAK dihapus.

### 2. DUDI global registry
- `CreateCompany(CreateCompanyRequest{Name, Sector, City, Address, ContactPerson})` — tujuan: entri registry global (lintas tenant — nilai jual utama).
- `VerifyCompany(Guid id)` — tujuan: tandai terverifikasi SA (usulan tenant dari FR-TEN-04).
- `MergeCompanies(Guid sourceId, Guid targetId) → MergeResultDto` — tujuan: dedup: pindahkan `TenantCompany`+`Placement` ke target, `source.MergedIntoId=target`, simpan `CompanyMergeHistory{SourceSnapshotJson}` (ber-riwayat, FR-SA-02); transaksional.
- `ListCompanies(CompanyFilter{Search?, Verified?, City?, Page})` · `SearchCompanies(string q, int limit=10)` — tujuan: registry + autocomplete linking tenant.

### 3. Plans & feature flags
- `CreatePlan(PlanRequest{Name, PriceMonthly, MaxStudents, MaxPlacements})` · `UpdatePlan(Guid id, ...)` — tujuan: paket langganan.
- `SetFeatureFlag(Guid planId, string key, bool enabled)` · `OverrideTenantFlag(Guid tenantId, string key, bool enabled)` — tujuan: flag per plan + override per tenant (FR-SA-03); key terdaftar enum (`GeotagAllowed, ParentDigest, ...`).
- `GetEffectiveFlags(Guid tenantId) → Dictionary<string,bool>` — tujuan: resolusi plan→override; dipanggil runtime (cache 60 dtk).

### 4. Ops
- `GetPlatformKpis() → KpiDto{ActiveTenants, ActiveStudents, JournalsToday, JournalFillRate, Mrr}` — tujuan: dashboard W5; MRR = Σ plan tenant aktif; query agregat.
- `GetSystemHealth() → HealthDto{QueueDepth, DlqCount, FailedJobs, OutboxUnpublished, ApiP95Ms?, DiskPct?}` — tujuan: panel W5; sumber: RabbitMQ mgmt API + Hangfire storage + outbox count (FR-SA-05).
- `QueryAuditLogs(AuditFilter{TenantId?, ActorId?, Entity?, From?, To?, Page}) → Paged<AuditDto>` — tujuan: viewer FR-SA-07; SA lihat semua, TenantAdmin versi tenant-scoped (endpoint terpisah policy `TenantAdmin`).

### 5. Billing
- `GenerateMonthlyInvoices()` — cron tgl 1 02:00 WIB. Tujuan: invoice per tenant aktif sesuai plan + event `InvoiceIssued` (email H4-E3); idempoten per (tenant, bulan) (FR-BIL-01).
- `GetInvoices(Guid? tenantId)` — SA semua / TenantAdmin miliknya. · `UploadPaymentProof(Guid invoiceId, string objectKey)` — tujuan: bukti transfer via presigned (FR-BIL-02); status `ProofUploaded`.
- `ConfirmPayment(Guid invoiceId)` — policy SaOnly. Tujuan: `Paid` + audit; tolak jika tanpa bukti.
- `CheckQuotaOnPlacement(Guid tenantId)` — tujuan: **aktifkan** guard (ganti stub H2-E1): hitung placement aktif vs `Plan.MaxPlacements` (+override) → lewat → `QuotaExceededException` → 402/409 dengan pesan; data lama tetap terbaca (FR-BIL-03).

### 6. Portfolio backend
- `GetMyPortfolio() → PortfolioDto{Headline, VerifiedCompetencies[], SampleJournals[], Certificate?, IsPublished, Slug?}` — tujuan: bahan editor siswa; kompetensi dari proyeksi jurnal Approved (H4).
- `UpdatePortfolio(UpdatePortfolioRequest{Headline≤120?, SampleJournalIds[]≤6})` — tujuan: kurasi; hanya jurnal Approved milik sendiri.
- `PublishPortfolio() → {Slug}` — tujuan: opt-in publik; **validasi payload publik tidak memuat NISN/kontak** (assert server-side, NFR-SEC-05); slug `nama-jurusan-tahun` unik; audit.
- `UnpublishPortfolio()` — tujuan: cabut kapan pun → publik 404.
- `GetPublicPortfolio(string slug) → PublicPortfolioDto` — **publik + rate limit + cacheable (Cache-Control 5 mnt)**. Tujuan: data W6 saja: nama, sekolah, jurusan, tahun, DUDI, durasi, kompetensi terverifikasi, sampel (thumbnail), status sertifikat — tanpa kontak/NISN.

## Acceptance Criteria

- Given wizard, When CreateTenant, Then admin baru bisa login & rubrik default ada (bukti gate M5).
- Given kuota 50 placement terpakai 50, When CreatePlacement, Then ditolak dengan pesan; ListJournals lama tetap 200.
- Given merge A→B, Then placement pindah, riwayat tercatat, `GET company A` → redirect/flag merged.
- Given publish lalu unpublish, Then `/p/{slug}` 200 → 404; payload publik lolos assert tanpa field sensitif.
- Given cron invoice 2× di bulan sama, Then invoice tetap 1 per tenant.

## DoD + verifikasi runner (high)

Build+test per kelompok sesuai prioritas → jalankan cron invoice manual (time-mock) → cek `GetSystemHealth` angka cocok kondisi nyata → `git diff --stat` → setor.
