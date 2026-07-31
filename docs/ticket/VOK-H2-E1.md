# VOK-H2-E1 — Seeder demo + endpoint periods/companies/placements

| Wilayah | Branch | Coder | Effort runner | Gate | Referensi |
|---|---|---|---|---|---|
| ENG-1 `backend/` | `h2-eng1-seeder-core-endpoints` | GPT-5.3-Codex | high | M1 | PRD FR-TEN-01..05, FR-X-04 |

## Tugas

Data demo realistis (1 perintah) + endpoint inti setup sekolah: periode, siswa (import CSV), link DUDI, placement. Semua endpoint sesuai OpenAPI beku H1, RBAC policy sesuai matrix 2.3 (policy dari H2-E3 — koordinasi nama policy, lihat [ASSUMPTION] wajib bila H2-E3 belum siap: pakai nama policy yang dideklarasikan di ticket VOK-H2-E3).

## Implementasi

### 1. Seeder — `Vokasia.Infrastructure/Seeding/`
- `SeedWilayahNpsnAsync()` — tujuan: isi tabel referensi wilayah (API emsifa) + sampel sekolah NPSN; hasil di-cache lokal (JSON committed) agar seed offline-able & deterministik.
- `SeedDemoDataAsync(SeedOptions opt = {Tenants:3, Companies:100, Students:900, Days:90})` — tujuan: 3 tenant (negeri besar, swasta kecil, luar Jawa), user per role, 100 DUDI global terverifikasi, 900 siswa, placement aktif, 90 hari `JournalSlot`+`JournalEntry` dengan distribusi realistis **termasuk skenario ghosting (≥3 hari kosong) dan rejected** — via Bogus, seed RNG tetap (deterministik), idempoten (cek marker sebelum insert).
- `ISeedRunner.RunAsync(string profile)` + CLI hook `dotnet run --project src/Vokasia.Api -- seed demo` — tujuan: 1 perintah dari clean state (NFR-MNT-04).

### 2. Endpoints Periode — `Api/Endpoints/Periods.cs` (policy: TenantAdmin/DeptHead)
- `CreatePeriod(CreatePeriodRequest{Name, StartDate, EndDate, ClassLevels[], Holidays[]?}) → PeriodDto` — tujuan: buat periode; validasi Start<End; audit.
- `UpdatePeriod(Guid id, UpdatePeriodRequest) → PeriodDto` — tujuan: ubah selama status Draft/Active; Closed → 409.
- `ListPeriods(PeriodFilter{Status?, Page, PageSize}) → Paged<PeriodDto>` — tujuan: daftar periode tenant.
- `SetHolidayCalendar(Guid periodId, List<HolidayDto{Date,Label}>)` — tujuan: kalender libur — dasar skip-libur cron H3.

### 3. Endpoints Siswa
- `ImportStudentsCsv(Guid? periodId, Stream csv, bool dryRun) → ImportResultDto{Imported, Errors[{Row, Column, Message}]}` — tujuan: import kolom umum Dapodik (nama, NISN, kelas, jurusan); baris valid masuk, invalid dilaporkan per baris (FR-TEN-02); `dryRun=true` validasi tanpa tulis.
- `CreateStudent(CreateStudentRequest{FullName, Nisn?, MajorId, Classroom}) → StudentDto` · `UpdateStudent(Guid id, UpdateStudentRequest)` · `ListStudents(StudentFilter{MajorId?, Classroom?, Search?, Page}) → Paged<StudentDto>` — tujuan: CRUD manual pelengkap import.

### 4. Endpoints User Sekolah
- `InviteSchoolUser(InviteUserRequest{Email, FullName, Role: Teacher|DeptHead|TenantAdmin})` — tujuan: buat user + email undangan set password.
- `AssignRole(Guid userId, string role)` · `ListSchoolUsers(Page)` · `DeactivateUser(Guid userId)` — tujuan: kelola user tenant; deactivate → revoke session (hook ke H2-E3).

### 5. Endpoints DUDI-tenant & Placement
- `LinkCompanyToTenant(Guid companyId)` — tujuan: tautkan DUDI global ke tenant.
- `ProposeCompany(ProposeCompanyRequest{Name, Sector, City, Address, ContactPerson}) → CompanyDto(IsVerified=false)` — tujuan: usulan DUDI baru, verifikasi oleh SA (H6).
- `SetCompanySlots(Guid companyId, Guid periodId, int slots)` — tujuan: kuota siswa per DUDI per periode.
- `CreatePlacement(CreatePlacementRequest{StudentId, CompanyId, PeriodId, TeacherId, MentorEmail}) → PlacementDto` — tujuan: penempatan; validasi slot tersedia + siswa belum placed; tulis event `PlacementCreated` ke outbox (consumer H4); panggil `CheckQuotaOnPlacement` (stub sampai H6 — selalu allow, tandai TODO-H6).
- `BulkCreatePlacements(List<CreatePlacementRequest>) → BulkResult{SuccessIds[], Errors[{Index, Message}]}` — tujuan: penempatan massal, hasil per baris.
- `AssignTeacher(Guid placementId, Guid teacherId)` · `ListPlacements(PlacementFilter{PeriodId, CompanyId?, Status?, Page})` · `GetPlacement(Guid id)` — tujuan: kelola & baca penempatan (scope role sesuai matrix).

## Acceptance Criteria

- Given clean DB, When `seed demo`, Then 3 tenant/900 siswa/90 hari terisi <5 mnt; ulang → tidak duplikat; skenario ghosting & rejected ada (dibuktikan query).
- Given CSV 10 baris (2 rusak), When import, Then 8 masuk + 2 error per baris presisi.
- Given slot DUDI penuh, When CreatePlacement, Then 409 + pesan.
- Given placement dibuat, Then `OutboxMessage{PlacementCreated}` tercatat 1 transaksi dengan placement.

## DoD + verifikasi runner (high)

Build+test per kelompok (seeder → periods → students → placements) → seed dari clean DB + ukur waktu → `git diff --stat` → setor.
