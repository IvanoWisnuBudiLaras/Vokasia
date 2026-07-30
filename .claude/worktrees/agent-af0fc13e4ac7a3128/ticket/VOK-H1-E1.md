# VOK-H1-E1 — Docker compose 7 service + skema DB & migration seluruh entitas

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-1 `backend/` + root | `h1-eng1-compose-migrations` | GPT-5.3-Codex | high | **M0** (kontrak beku sore H1) | PRD §0, §2.1–2.4, AGENTS.md |

## Tugas

Menghidupkan seluruh infra via docker-compose dan membekukan skema DB: semua entitas domain + konfigurasi EF Core + migration `Initial` + index dasar. Ini fondasi kontrak M0 — setelah direview VPM+Dev sore H1, skema beku.

## Implementasi

### 1. `docker-compose.yml` (root — sudah ada draft, lengkapi)
7 service: `api`, `worker`, `frontend`, `postgres:17`, `redis:7`, `rabbitmq:4-management`, `minio`. Tiap service: healthcheck, restart policy `unless-stopped`, volume persisten (postgres, minio, rabbitmq), env dari `.env`, network internal. Port keluar minimal: frontend 3000, api 8080 (dev), rabbitmq mgmt 15672 (dev).

### 2. Entitas domain — `backend/src/Vokasia.Domain/Entities/`
Semua tenant-scoped punya `TenantId (Guid)` KECUALI yang bertanda [global]:

- `Tenant` — `Id, SchoolName, Npsn, Address, PlanId, IsActive, CreatedAt`
- `AppUser` (Identity) — `Id, TenantId?, FullName, Role, IsActive` (SuperAdmin & IndustryMentor: `TenantId=null`)
- `Company` [global] — `Id, Name, Sector, Address, City, ContactPerson, IsVerified, MergedIntoId?`
- `TenantCompany` — link tenant↔company + `SlotsPerPeriod`
- `Period` — `Id, Name, StartDate, EndDate, ClassLevels, Status(Draft|Active|Assessment|Closed)`
- `Holiday` — `PeriodId, Date, Label`
- `Student` — `Id, UserId?, FullName, Nisn?, MajorId, Classroom` (data minimal — NFR-SEC-05)
- `Major` + `Competency` — `MajorId, Name` (daftar kompetensi per jurusan)
- `Placement` — `Id, StudentId, CompanyId, PeriodId, TeacherId, MentorUserId?, Status`
- `JournalSlot` — `Id, PlacementId, Date, Status(Empty|Filled)`
- `JournalEntry` — `Id, SlotId, PlacementId, Text(≤500), Status(Submitted|Approved|Rejected), MentorNote?, ApprovedAt?`
- `JournalPhoto` — `Id, JournalEntryId, ObjectKey, ThumbKey?, Status(Pending|Processed)`
- `JournalCompetency` — many-to-many entry↔competency
- `TeacherComment` — `JournalEntryId, TeacherId, Text, CreatedAt`
- `StudentDailyStatus` — `StudentId, PeriodId, Date, Rag(Green|Amber|Red), Streak`
- `Visit` — `Id, PlacementId, TeacherId, Date, Notes, PhotoKey?, SignatureKey?`
- `RubricTemplate` + `RubricAspect` — `Name, Kind(Teknis|Softskill|Kehadiran), Weight` (Σweight=100)
- `Assessment` + `AssessmentScore` — `PlacementId, AspectId, ScoredBy(Mentor|Teacher), Value(0–100)`; `Assessment.FinalScore?, IsFinal, FinalizedAt?`
- `Certificate` — `Id, PlacementId, CertCode(unik, publik), PdfKey, IssuedAt`
- `Portfolio` — `Id, StudentId, Slug(unik), Headline?, IsPublished, SampleJournalIds`
- `Plan` [global] — `Id, Name, PriceMonthly, MaxStudents, MaxPlacements` + `FeatureFlag(PlanId|TenantId, Key, Enabled)`
- `Invoice` — `Id, TenantId, PeriodMonth, Amount, Status(Issued|ProofUploaded|Paid), ProofKey?`
- `Notification` — `Id, UserId, Type, PayloadJson, IsRead, CreatedAt`
- `AuditLog` — `Id, TenantId?, ActorUserId, ActingAsUserId?, Action, Entity, EntityId, MetaJson, CreatedAt`
- `OutboxMessage` — `Id, Type, PayloadJson, OccurredAt, PublishedAt?` · `ProcessedMessage` — `ConsumerName, MessageId, ProcessedAt` (PK gabungan)

### 3. Fungsi/konfigurasi — `Vokasia.Infrastructure`

- `VokasiaDbContext.OnModelCreating(ModelBuilder b)` — tujuan: konfigurasi seluruh entitas: PK/FK, unique (`Certificate.CertCode`, `Portfolio.Slug`, `(SlotId)` di JournalEntry), enum→string, presisi decimal.
- `ApplyTenantQueryFilters(ModelBuilder b, ITenantContext ctx)` — tujuan: **stub** global query filter `e.TenantId == ctx.TenantId` untuk semua entitas tenant-scoped (diaktifkan penuh H2-E3); tandai `// ACTIVATED-H2E3`.
- `ITenantContext { Guid? TenantId; Guid? UserId; string Role; }` (`Vokasia.Domain`) — tujuan: kontrak konteks tenant per-request, diisi middleware H2-E3.
- `AddVokasiaInfrastructure(IServiceCollection s, IConfiguration cfg)` — tujuan: registrasi DbContext (Npgsql), MinIO client, Redis multiplexer — satu titik DI.
- Migration `Initial` — tujuan: seluruh skema; **harus aman dijalankan ulang** (`dotnet ef database update` idempoten).
- Index: `JournalSlot(PlacementId,Date)`, `JournalEntry(PlacementId,Status)`, `StudentDailyStatus(PeriodId,Date,Rag)`, `Placement(PeriodId)`, `AuditLog(TenantId,CreatedAt)` — tujuan: query dashboard W3/W5 tanpa scan.

## Acceptance Criteria

- Given clean state, When `docker compose up -d`, Then 7 service `healthy`.
- Given DB kosong, When `dotnet ef database update`, Then semua tabel+FK+index terbentuk; jalankan ulang → tanpa error.
- Given filter stub terpasang, When test `TenantFilterSmokeTest` query tanpa tenant context, Then hasil kosong/exception terkendali (bukti mekanisme hidup).

## DoD + verifikasi runner (high)

`dotnet build` → `dotnet test` → `docker compose up -d` + cek `docker compose ps` semua healthy → `dotnet ef database update` 2× → `git diff --stat` hanya `backend/` + root compose/env → setor + skema ke VPM untuk gate M0.
