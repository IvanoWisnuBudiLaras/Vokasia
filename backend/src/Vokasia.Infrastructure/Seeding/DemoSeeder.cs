using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Seeding;

public record SeedOptions(int Tenants = 3, int Companies = 100, int StudentsTotal = 900, int Days = 90);

/// <summary>
/// VOK-H2-E1 §1: data demo realistis, 1 perintah, idempoten, deterministik (Randomizer.Seed tetap).
/// [ASSUMPTION]: SeedWilayahNpsnAsync (API emsifa) DILEWATI — tidak ada entitas Wilayah di skema
/// H1-E1 (Tenant.Address/City cukup string bebas untuk MVP), dan lingkungan CI/sandbox tidak selalu
/// punya akses jaringan keluar. Dicatat sebagai catatan, bukan diimplementasikan diam-diam.
/// </summary>
public static class DemoSeeder
{
    private const string MarkerNpsn = "10000001"; // NPSN tenant pertama seed — dipakai cek idempotensi.

    public static async Task<string> SeedDemoDataAsync(VokasiaDbContext db, UserManager<AppUser> userManager, SeedOptions? opt = null, CancellationToken ct = default)
    {
        opt ??= new SeedOptions();
        Randomizer.Seed = new Random(20260721); // deterministik.

        var already = await db.Tenants.AnyAsync(t => t.Npsn == MarkerNpsn, ct);
        if (already)
        {
            return "SKIP: demo data sudah ada (marker NPSN ditemukan) — idempoten, tidak menulis ulang.";
        }

        var tenantProfiles = new[]
        {
            ("SMK Negeri 1 Makmur", MarkerNpsn, "Kota Makmur", "Jawa Barat"),
            ("SMK Swasta Harapan Bangsa", "10000002", "Kota Kecil", "Jawa Tengah"),
            ("SMK Negeri 3 Nusantara", "10000003", "Kota Timur", "Sulawesi Selatan"),
        }.Take(opt.Tenants).ToArray();

        var faker = new Faker("id_ID");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = today.AddDays(-opt.Days);

        var companies = new List<Company>();
        for (var i = 0; i < opt.Companies; i++)
        {
            companies.Add(new Company
            {
                Id = Guid.NewGuid(),
                Name = $"{faker.Company.CompanyName()} {i + 1}",
                Sector = faker.PickRandom("Teknologi", "Manufaktur", "Perhotelan", "Otomotif", "Retail", "Kesehatan"),
                City = faker.Address.City(),
                ContactPerson = faker.Name.FullName(),
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }
        db.Companies.AddRange(companies);
        await db.SaveChangesAsync(ct);

        var studentsPerTenant = opt.StudentsTotal / tenantProfiles.Length;
        var totalPlacements = 0;
        var totalJournalEntries = 0;

        foreach (var (name, npsn, city, region) in tenantProfiles)
        {
            var tenant = new Tenant { Id = Guid.NewGuid(), SchoolName = name, Npsn = npsn, City = city, Address = $"{city}, {region}", IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
            db.Tenants.Add(tenant);

            var majors = new[] { "Teknik Komputer Jaringan", "Rekayasa Perangkat Lunak", "Akuntansi" }
                .Select(n => new Major { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = n }).ToList();
            db.Majors.AddRange(majors);

            var period = new Period
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "PKL Ganjil 2026",
                StartDate = periodStart,
                EndDate = today,
                ClassLevels = "XII",
                Status = PeriodStatus.Active,
            };
            db.Periods.Add(period);

            // Kalender libur sederhana: setiap Minggu selama rentang periode.
            for (var d = periodStart; d <= today; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday)
                {
                    db.Holidays.Add(new Holiday { Id = Guid.NewGuid(), TenantId = tenant.Id, PeriodId = period.Id, Date = d, Label = "Minggu" });
                }
            }

            // Teacher + DeptHead + TenantAdmin demo (password statis khusus dev — [ASSUMPTION] SEED_DEFAULT_PASSWORD).
            var admin = await CreateDemoUserAsync(userManager, tenant.Id, $"admin@{npsn}.vokasia.demo", "Admin " + name, UserRole.TenantAdmin, ct);
            var depthead = await CreateDemoUserAsync(userManager, tenant.Id, $"depthead@{npsn}.vokasia.demo", "Kepala Jurusan " + name, UserRole.DeptHead, ct);
            var teachers = new List<AppUser>();
            for (var i = 0; i < 5; i++)
            {
                teachers.Add(await CreateDemoUserAsync(userManager, tenant.Id, $"guru{i}@{npsn}.vokasia.demo", faker.Name.FullName(), UserRole.Teacher, ct));
            }

            await db.SaveChangesAsync(ct);

            var students = new List<Student>();
            for (var i = 0; i < studentsPerTenant; i++)
            {
                students.Add(new Student
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    FullName = faker.Name.FullName(),
                    Nisn = faker.Random.ReplaceNumbers("00##########"),
                    MajorId = faker.PickRandom(majors).Id,
                    Classroom = faker.PickRandom("XII TKJ 1", "XII TKJ 2", "XII RPL 1", "XII AK 1"),
                });
            }
            db.Students.AddRange(students);
            await db.SaveChangesAsync(ct);

            // Slot DUDI cukup longgar (tidak ada skenario "penuh" di seed demo — itu diuji unit test terpisah).
            var poolCompanies = faker.PickRandom(companies, Math.Min(30, companies.Count)).ToList();
            foreach (var c in poolCompanies)
            {
                if (!await db.TenantCompanies.AnyAsync(tc => tc.TenantId == tenant.Id && tc.CompanyId == c.Id, ct))
                {
                    db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.Id, CompanyId = c.Id });
                }
                db.CompanySlots.Add(new CompanySlot { Id = Guid.NewGuid(), TenantId = tenant.Id, CompanyId = c.Id, PeriodId = period.Id, Slots = 50 });
            }
            await db.SaveChangesAsync(ct);

            var placements = new List<Placement>();
            var rag = new List<StudentDailyStatus>();
            var ghostingIndex = 0;

            for (var i = 0; i < students.Count; i++)
            {
                var student = students[i];
                var company = faker.PickRandom(poolCompanies);
                var teacher = faker.PickRandom(teachers);
                var isGhosting = ghostingIndex < students.Count * 0.05; // ~5% skenario ghosting
                ghostingIndex++;

                var placement = new Placement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    StudentId = student.Id,
                    CompanyId = company.Id,
                    PeriodId = period.Id,
                    TeacherId = teacher.Id,
                    Status = PlacementStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                placements.Add(placement);

                var streak = 0;
                var rowsThisStudent = new List<(JournalSlot Slot, JournalEntry? Entry)>();

                for (var d = periodStart; d <= today; d = d.AddDays(1))
                {
                    if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    {
                        continue; // hari kerja saja.
                    }

                    var daysAgo = today.DayNumber - d.DayNumber;
                    var isRecentGhostWindow = isGhosting && daysAgo <= 4; // >=3 hari kerja kosong terbaru.

                    var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenant.Id, PlacementId = placement.Id, Date = d, Status = JournalSlotStatus.Empty };

                    JournalEntry? entry = null;
                    if (!isRecentGhostWindow && faker.Random.Double() > 0.03) // ~97% hari kerja terisi di luar window ghosting
                    {
                        var isRejected = faker.Random.Double() < 0.05; // ~5% rejected
                        entry = new JournalEntry
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            SlotId = slot.Id,
                            PlacementId = placement.Id,
                            Text = faker.Lorem.Sentence(faker.Random.Int(6, 15)),
                            Status = isRejected ? JournalEntryStatus.Rejected : JournalEntryStatus.Approved,
                            MentorNote = isRejected ? "Catatan belum lengkap, tolong revisi." : null,
                            // PENTING: DateOnly.ToDateTime(TimeOnly) -> DateTime Kind=Unspecified; konversi implisit
                            // ke DateTimeOffset memakai timezone LOKAL mesin (WIB=+07:00), tapi Npgsql "timestamp
                            // with time zone" cuma terima offset 0 (UTC) — wajib new DateTimeOffset(..., TimeSpan.Zero).
                            SubmittedAt = new DateTimeOffset(d.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(16))), TimeSpan.Zero),
                            ApprovedAt = isRejected ? null : new DateTimeOffset(d.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(18))), TimeSpan.Zero),
                        };
                        slot.Status = JournalSlotStatus.Filled;
                        streak = isRejected ? 0 : streak + 1;
                    }
                    else
                    {
                        streak = 0;
                    }

                    rowsThisStudent.Add((slot, entry));
                }

                db.JournalSlots.AddRange(rowsThisStudent.Select(r => r.Slot));
                db.JournalEntries.AddRange(rowsThisStudent.Where(r => r.Entry is not null).Select(r => r.Entry!));
                totalJournalEntries += rowsThisStudent.Count(r => r.Entry is not null);

                var lastDaysEmpty = rowsThisStudent.OrderByDescending(r => r.Slot.Date).TakeWhile(r => r.Entry is null).Count();
                rag.Add(new StudentDailyStatus
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    StudentId = student.Id,
                    PeriodId = period.Id,
                    Date = today,
                    Rag = lastDaysEmpty >= 3 ? RagStatus.Red : lastDaysEmpty >= 1 ? RagStatus.Amber : RagStatus.Green,
                    Streak = streak,
                });

                // Batch per 50 siswa supaya change tracker tidak membengkak (target <5 mnt, NFR-MNT-04).
                if (placements.Count % 50 == 0)
                {
                    db.Placements.AddRange(placements);
                    db.StudentDailyStatuses.AddRange(rag);
                    await db.SaveChangesAsync(ct);
                    totalPlacements += placements.Count;
                    placements.Clear();
                    rag.Clear();
                }
            }

            if (placements.Count > 0)
            {
                db.Placements.AddRange(placements);
                db.StudentDailyStatuses.AddRange(rag);
                await db.SaveChangesAsync(ct);
                totalPlacements += placements.Count;
            }
        }

        return $"OK: {tenantProfiles.Length} tenant, {totalPlacements} placement/siswa, {totalJournalEntries} journal entries, {opt.Companies} DUDI.";
    }

    private static async Task<AppUser> CreateDemoUserAsync(UserManager<AppUser> userManager, Guid tenantId, string email, string fullName, UserRole role, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new AppUser { UserName = email, Email = email, FullName = fullName, TenantId = tenantId, Role = role, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, "Demo-Passw0rd!"); // [ASSUMPTION] password seed dev tetap, JANGAN dipakai produksi.
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Gagal membuat user demo {email}: {string.Join(",", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }
}
