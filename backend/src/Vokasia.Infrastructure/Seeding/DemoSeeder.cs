using Bogus;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Seeding;

public record SeedOptions(int Tenants = 3, int Companies = 100, int StudentsPerTenant = 300, int Days = 90);

/// <summary>
/// DemoSeeder: data demo realistis Indonesia, 1 perintah, idempoten & pendukung reset data bersih.
/// </summary>
public static class DemoSeeder
{
    private const string CertificateScenarioName = "DEMO-CERTIFICATE";
    private const string MarkerNpsn = "20101001"; // NPSN SMKN 1 Jakarta — dipakai marker idempotensi.

    public static async Task<string> SeedDemoDataAsync(VokasiaDbContext db, UserManager<AppUser> userManager, SeedOptions? opt = null, bool forceReset = false, CancellationToken ct = default)
    {
        opt ??= new SeedOptions();
        Randomizer.Seed = new Random(20260731);

        if (forceReset)
        {
            // Wipe existing data cleanly for fresh re-seed
            db.SentEmails.RemoveRange(db.SentEmails);
            db.MentorInvites.RemoveRange(db.MentorInvites);
            db.ProcessedMessages.RemoveRange(db.ProcessedMessages);
            db.OutboxMessages.RemoveRange(db.OutboxMessages);
            db.AuditLogs.RemoveRange(db.AuditLogs);
            db.Notifications.RemoveRange(db.Notifications);
            db.Invoices.RemoveRange(db.Invoices);
            db.FeatureFlags.RemoveRange(db.FeatureFlags);
            db.Plans.RemoveRange(db.Plans);
            db.ExportRequests.RemoveRange(db.ExportRequests);
            db.Portfolios.RemoveRange(db.Portfolios);
            db.Certificates.RemoveRange(db.Certificates);
            db.AssessmentScores.RemoveRange(db.AssessmentScores);
            db.Assessments.RemoveRange(db.Assessments);
            db.RubricAspects.RemoveRange(db.RubricAspects);
            db.RubricTemplates.RemoveRange(db.RubricTemplates);
            db.Visits.RemoveRange(db.Visits);
            db.StudentDailyStatuses.RemoveRange(db.StudentDailyStatuses);
            db.TeacherComments.RemoveRange(db.TeacherComments);
            db.JournalCompetencies.RemoveRange(db.JournalCompetencies);
            db.JournalPhotos.RemoveRange(db.JournalPhotos);
            db.JournalEntries.RemoveRange(db.JournalEntries);
            db.JournalSlots.RemoveRange(db.JournalSlots);
            db.Placements.RemoveRange(db.Placements);
            db.Students.RemoveRange(db.Students);
            db.Competencies.RemoveRange(db.Competencies);
            db.Majors.RemoveRange(db.Majors);
            db.Holidays.RemoveRange(db.Holidays);
            db.Periods.RemoveRange(db.Periods);
            db.CompanySlots.RemoveRange(db.CompanySlots);
            db.TenantCompanies.RemoveRange(db.TenantCompanies);
            db.CompanyMergeHistories.RemoveRange(db.CompanyMergeHistories);
            db.Companies.RemoveRange(db.Companies);
            db.Tenants.RemoveRange(db.Tenants);

            var allUsers = await userManager.Users.ToListAsync(ct);
            foreach (var u in allUsers)
            {
                await userManager.DeleteAsync(u);
            }
            await db.SaveChangesAsync(ct);
        }
        else
        {
            var already = await db.Tenants.AnyAsync(t => t.Npsn == MarkerNpsn, ct);
            if (already)
            {
                return "SKIP: demo data sudah ada (marker NPSN 20101001 ditemukan) — idempoten, tidak menulis ulang.";
            }
        }

        // 1. Seed SuperAdmin Accounts (Global platform admins)
        await CreateDemoUserAsync(userManager, null, "superadmin@vokasia.id", "Super Admin Vokasia", UserRole.SuperAdmin, ct);
        await CreateDemoUserAsync(userManager, null, "superadmin.backup@vokasia.example", "Super Admin Vokasia (Backup)", UserRole.SuperAdmin, ct);

        // 2. Seed Subscription Plans for SaaS MRR Calculations
        var planStarter = new Plan { Id = Guid.NewGuid(), Name = "Starter SMK", PriceMonthly = 499000m, MaxStudents = 200, MaxPlacements = 200 };
        var planPro = new Plan { Id = Guid.NewGuid(), Name = "Professional SMK", PriceMonthly = 1499000m, MaxStudents = 1000, MaxPlacements = 1000 };
        var planEnterprise = new Plan { Id = Guid.NewGuid(), Name = "Enterprise Multi-Campuses", PriceMonthly = 3999000m, MaxStudents = 5000, MaxPlacements = 5000 };
        db.Plans.AddRange(planStarter, planPro, planEnterprise);
        await db.SaveChangesAsync(ct);

        // 3. Seed Realistic DUDI Companies
        var realCompanies = Enumerable.Range(1, 100)
            .Select(i => ($"PT Contoh Teknologi {i:D3}", "Teknologi", "Kota Contoh", $"Mentor DUDI Contoh {i:D3}", $"mentor{i:D3}@dudi.example"))
            .ToArray();
        var companies = new List<Company>();
        var mentors = new List<AppUser>();

        foreach (var (compName, sector, city, mentorName, mentorEmail) in realCompanies)
        {
            var company = new Company
            {
                Id = Guid.NewGuid(),
                Name = compName,
                Sector = sector,
                City = city,
                ContactPerson = mentorName,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            companies.Add(company);

            var mentorIndex = mentors.Count + 1;
            var mentorUser = await CreateDemoUserAsync(userManager, null, $"mentor{mentorIndex:D3}@dudi.example", mentorName, UserRole.IndustryMentor, ct);
            mentors.Add(mentorUser);
        }
        db.Companies.AddRange(companies);
        await db.SaveChangesAsync(ct);

        // 4. Seed School Tenants (Realistic Indonesian SMK)
        var tenantProfiles = new[]
        {
            (Name: "SMK Contoh 1", Npsn: "20101001", City: "Kota Jakarta Pusat", Region: "DKI Jakarta", AdminEmail: "admin01@smkcontoh.example", DeptHeadEmail: "kepala01@smkcontoh.example", TeacherEmail: "guru01@smkcontoh.example", PlanId: planPro.Id),
            (Name: "SMK Contoh 2", Npsn: "20202002", City: "Kota Bandung", Region: "Jawa Barat", AdminEmail: "admin02@smkcontoh.example", DeptHeadEmail: "kepala02@smkcontoh.example", TeacherEmail: "guru02@smkcontoh.example", PlanId: planPro.Id),
            (Name: "SMK Contoh 3", Npsn: "20303003", City: "Kota Surabaya", Region: "Jawa Timur", AdminEmail: "admin03@smkcontoh.example", DeptHeadEmail: "kepala03@smkcontoh.example", TeacherEmail: "guru03@smkcontoh.example", PlanId: planStarter.Id)
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = today.AddDays(-opt.Days);
        var totalPlacements = 0;
        var totalJournalEntries = 0;

        var indonesianStudents = Enumerable.Range(1, opt.StudentsPerTenant)
            .Select(i => ($"Siswa Contoh {i:D3}", $"siswa{i:D3}@smkcontoh.example"))
            .ToArray();
        var faker = new Faker("id_ID");

        for (var profileIndex = 0; profileIndex < tenantProfiles.Length; profileIndex++)
        {
            var profile = tenantProfiles[profileIndex];
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                SchoolName = profile.Name,
                Npsn = profile.Npsn,
                City = profile.City,
                Address = $"Jl. Pendidikan No. 12, {profile.City}, {profile.Region}",
                IsActive = true,
                PlanId = profile.PlanId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            db.Tenants.Add(tenant);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Amount = profile.PlanId == planStarter.Id ? 499000m : 1499000m,
                PeriodMonth = new DateOnly(today.Year, today.Month, 1),
                Status = InvoiceStatus.Paid
            };
            db.Invoices.Add(invoice);

            var majors = new[] { "Teknik Komputer dan Jaringan", "Rekayasa Perangkat Lunak", "Akuntansi dan Keuangan Lembaga" }
                .Select(n => new Major { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = n }).ToList();
            db.Majors.AddRange(majors);

            var period = new Period
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "PKL Periode Ganjil 2026",
                StartDate = periodStart,
                EndDate = today,
                ClassLevels = "XII",
                Status = PeriodStatus.Active,
            };
            db.Periods.Add(period);
            var demoRubric = new RubricTemplate
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Rubrik Demo PKL", IsDefault = true,
                Aspects =
                [
                    new RubricAspect { Id = Guid.NewGuid(), Name = "Teknis", Kind = RubricAspectKind.Teknis, Weight = 60 },
                    new RubricAspect { Id = Guid.NewGuid(), Name = "Softskill", Kind = RubricAspectKind.Softskill, Weight = 30 },
                    new RubricAspect { Id = Guid.NewGuid(), Name = "Kehadiran", Kind = RubricAspectKind.Kehadiran, Weight = 10 },
                ],
            };
            db.RubricTemplates.Add(demoRubric);

            for (var d = periodStart; d <= today; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday)
                {
                    db.Holidays.Add(new Holiday { Id = Guid.NewGuid(), TenantId = tenant.Id, PeriodId = period.Id, Date = d, Label = "Hari Minggu" });
                }
            }

            await CreateDemoUserAsync(userManager, tenant.Id, profile.AdminEmail, $"Admin {profile.Name}", UserRole.TenantAdmin, ct);
            await CreateDemoUserAsync(userManager, tenant.Id, profile.DeptHeadEmail, $"Kepala Jurusan {profile.Name}", UserRole.DeptHead, ct);
            var teacherUser = await CreateDemoUserAsync(userManager, tenant.Id, profile.TeacherEmail, $"Pembimbing {profile.Name}", UserRole.Teacher, ct);

            await db.SaveChangesAsync(ct);

            var students = new List<Student>();
            for (var i = 0; i < indonesianStudents.Length; i++)
            {
                var (baseName, _) = indonesianStudents[i];
                var isCertificateScenario = profileIndex == 0 && i == 4;
                var stName = isCertificateScenario ? CertificateScenarioName : baseName;
                var stEmail = isCertificateScenario
                    ? "demo-certificate@smkcontoh.example"
                    : $"siswa{profileIndex + 1:D2}-{i + 1:D3}@smkcontoh.example";
                var studentId = Guid.NewGuid();

                var student = new Student
                {
                    Id = studentId,
                    TenantId = tenant.Id,
                    FullName = stName,
                    Nisn = $"{profile.Npsn}{i + 1:D3}",
                    MajorId = faker.PickRandom(majors).Id,
                    Classroom = faker.PickRandom("XII TKJ 1", "XII RPL 1", "XII AKL 1"),
                };
                var stUser = await CreateDemoUserAsync(userManager, tenant.Id, stEmail, stName, UserRole.Student, ct);
                student.UserId = stUser.Id;
                students.Add(student);
            }
            db.Students.AddRange(students);
            await db.SaveChangesAsync(ct);

            var poolCompanies = companies.Take(5).ToList();
            foreach (var c in poolCompanies)
            {
                if (!await db.TenantCompanies.AnyAsync(tc => tc.TenantId == tenant.Id && tc.CompanyId == c.Id, ct))
                {
                    db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.Id, CompanyId = c.Id });
                }
                db.CompanySlots.Add(new CompanySlot { Id = Guid.NewGuid(), TenantId = tenant.Id, CompanyId = c.Id, PeriodId = period.Id, Slots = 20 });
            }
            await db.SaveChangesAsync(ct);

            var placements = new List<Placement>();
            var ragList = new List<StudentDailyStatus>();

            for (var i = 0; i < students.Count; i++)
            {
                var student = students[i];
                var companyIdx = i % poolCompanies.Count;
                var company = poolCompanies[companyIdx];
                var mentorUser = mentors[companyIdx];

                var placement = new Placement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    StudentId = student.Id,
                    CompanyId = company.Id,
                    PeriodId = period.Id,
                    TeacherId = teacherUser.Id,
                    MentorUserId = mentorUser.Id,
                    MentorEmail = mentorUser.Email,
                    Status = PlacementStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                placements.Add(placement);

                var streak = 0;
                var journalRows = new List<(JournalSlot Slot, JournalEntry? Entry)>();

                for (var d = periodStart; d <= today; d = d.AddDays(1))
                {
                    if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

                    var slot = new JournalSlot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        PlacementId = placement.Id,
                        Date = d,
                        Status = JournalSlotStatus.Empty
                    };

                    JournalEntry? entry = null;
                    var recentWorkingDays = Enumerable.Range(0, 5)
                        .Select(offset => today.AddDays(-offset))
                        .Where(date => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                        .ToArray();
                    var forcedMissing = (i == 1 && recentWorkingDays.Contains(d)) || (i == 2 && d == recentWorkingDays[0]);
                    var forcedHealthy = i == 0;
                    var forcedRejected = i == 3 && d >= periodStart && d <= periodStart.AddDays(1);
                    if (!forcedMissing && (forcedHealthy || forcedRejected || faker.Random.Double() > 0.05))
                    {
                        entry = new JournalEntry
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            SlotId = slot.Id,
                            PlacementId = placement.Id,
                            Text = $"Mengikuti kegiatan pengerjaan tugas harian di {company.Name}. Melakukan pemeliharaan jaringan dan dokumentasi.",
                            Status = forcedRejected ? JournalEntryStatus.Rejected : JournalEntryStatus.Approved,
                            MentorNote = forcedRejected ? "Lengkapi langkah kerja dan bukti kegiatan sebelum kirim ulang." : null,
                            SubmittedAt = new DateTimeOffset(d.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(16))), TimeSpan.Zero),
                            ApprovedAt = new DateTimeOffset(d.ToDateTime(TimeOnly.FromTimeSpan(TimeSpan.FromHours(18))), TimeSpan.Zero)
                        };
                        slot.Status = JournalSlotStatus.Filled;
                        streak++;
                    }
                    else
                    {
                        streak = 0;
                    }

                    journalRows.Add((slot, entry));
                }

                db.JournalSlots.AddRange(journalRows.Select(r => r.Slot));
                db.JournalEntries.AddRange(journalRows.Where(r => r.Entry != null).Select(r => r.Entry!));
                totalJournalEntries += journalRows.Count(r => r.Entry != null);

                var lastDaysEmpty = journalRows.OrderByDescending(r => r.Slot.Date).TakeWhile(r => r.Entry is null).Count();
                ragList.Add(new StudentDailyStatus
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    StudentId = student.Id,
                    PeriodId = period.Id,
                    Date = today,
                    Rag = lastDaysEmpty >= 3 ? RagStatus.Red : lastDaysEmpty >= 1 ? RagStatus.Amber : RagStatus.Green,
                    Streak = streak
                });
            }

            db.Placements.AddRange(placements);
            db.StudentDailyStatuses.AddRange(ragList);
            if (placements.Count > 4)
            {
                var certificatePlacement = placements[4];
                certificatePlacement.Status = PlacementStatus.Completed;
                period.Status = PeriodStatus.Assessment;
                var certificateAssessment = new Assessment
                {
                    Id = Guid.NewGuid(), TenantId = tenant.Id, PlacementId = certificatePlacement.Id,
                    RubricTemplateId = demoRubric.Id, FinalScore = 88m, IsFinal = true,
                    FinalizedAt = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-1).AddHours(12), TimeSpan.Zero),
                };
                db.Assessments.Add(certificateAssessment);
                foreach (var aspect in demoRubric.Aspects)
                {
                    db.AssessmentScores.Add(new AssessmentScore
                    {
                        Id = Guid.NewGuid(), AssessmentId = certificateAssessment.Id, RubricAspectId = aspect.Id,
                        ScoredBy = aspect.Kind is RubricAspectKind.Teknis or RubricAspectKind.Kehadiran ? ScoredBy.Mentor : ScoredBy.Teacher,
                        ScoredByUserId = aspect.Kind is RubricAspectKind.Teknis or RubricAspectKind.Kehadiran ? mentors[4].Id : teacherUser.Id,
                        Value = 88m,
                    });
                }
                db.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(), Type = "CertificateRequested",
                    PayloadJson = JsonSerializer.Serialize(new { PlacementId = certificatePlacement.Id, TenantId = tenant.Id }),
                });
            }
            await db.SaveChangesAsync(ct);
            totalPlacements += placements.Count;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantProfiles[0].PlanId,
            ActorUserId = Guid.NewGuid(),
            Action = "PlatformDataSeeded",
            Entity = "DemoSeeder",
            EntityId = Guid.NewGuid().ToString(),
            MetaJson = "{\"status\":\"Success\",\"message\":\"Real Indonesian demo data populated successfully\"}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return $"RESET & SEED DEMO SUCCESSFUL: {tenantProfiles.Length} fictional schools, {totalPlacements} students, {realCompanies.Length} DUDI, {totalJournalEntries} journal entries.";
    }

    private static async Task<AppUser> CreateDemoUserAsync(UserManager<AppUser> userManager, Guid? tenantId, string email, string fullName, UserRole role, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            TenantId = tenantId,
            Role = role,
            EmailConfirmed = true,
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, "Demo-Passw0rd!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Gagal membuat user demo {email}: {string.Join(",", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }
}
