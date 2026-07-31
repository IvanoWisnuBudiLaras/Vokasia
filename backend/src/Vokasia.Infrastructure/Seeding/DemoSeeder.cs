using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Seeding;

public record SeedOptions(int Tenants = 3, int Companies = 10, int StudentsPerTenant = 30, int Days = 60);

/// <summary>
/// DemoSeeder: data demo realistis Indonesia, 1 perintah, idempoten & pendukung reset data bersih.
/// </summary>
public static class DemoSeeder
{
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
        await CreateDemoUserAsync(userManager, null, "ifogram2024@gmail.com", "Ifogram SuperAdmin", UserRole.SuperAdmin, ct);
        await CreateDemoUserAsync(userManager, null, "superadmin@vokasia.id", "Super Admin Vokasia", UserRole.SuperAdmin, ct);
        await CreateDemoUserAsync(userManager, null, "superadmin@gmail.com", "Super Admin Vokasia (Backup)", UserRole.SuperAdmin, ct);

        // 2. Seed Subscription Plans for SaaS MRR Calculations
        var planStarter = new Plan { Id = Guid.NewGuid(), Name = "Starter SMK", PriceMonthly = 499000m, MaxStudents = 200, MaxPlacements = 200 };
        var planPro = new Plan { Id = Guid.NewGuid(), Name = "Professional SMK", PriceMonthly = 1499000m, MaxStudents = 1000, MaxPlacements = 1000 };
        var planEnterprise = new Plan { Id = Guid.NewGuid(), Name = "Enterprise Multi-Campuses", PriceMonthly = 3999000m, MaxStudents = 5000, MaxPlacements = 5000 };
        db.Plans.AddRange(planStarter, planPro, planEnterprise);
        await db.SaveChangesAsync(ct);

        // 3. Seed Realistic DUDI Companies
        var realCompanies = new[]
        {
            ("PT Telkom Indonesia (Persero) Tbk", "Teknologi", "Kota Bandung", "Alvano Mentor DUDI", "mr.alvano11@gmail.com"),
            ("PT Astra International Tbk", "Otomotif", "Kota Jakarta Pusat", "Agus Prasetyo", "mentor.agus@astra.co.id"),
            ("PT Bank Central Asia Tbk", "Perbankan", "Kota Jakarta Pusat", "Rina Kartika", "mentor.rina@bca.co.id"),
            ("PT Indosat Ooredoo Hutchison Tbk", "Telekomunikasi", "Kota Jakarta Selatan", "Doni Kusuma", "mentor.doni@indosat.com"),
            ("PT Tokopedia", "E-Commerce", "Kota Jakarta Selatan", "Hendra Kurniawan", "mentor.hendra@tokopedia.com"),
            ("PT GoTo Gojek Tokopedia Tbk", "Teknologi", "Kota Jakarta Selatan", "Suryo Saputra", "mentor.suryo@goto.com"),
            ("PT XL Axiata Tbk", "Telekomunikasi", "Kota Jakarta Selatan", "Anita Wijaya", "mentor.anita@xl.co.id"),
            ("PT PLN (Persero)", "Energi", "Kota Surabaya", "Tri Haryanto", "mentor.tri@pln.co.id"),
            ("PT Kalbe Farma Tbk", "Kesehatan", "Kota Jakarta Timur", "Dr. Ratna Juwita", "mentor.ratna@kalbe.co.id"),
            ("PT Kereta Api Indonesia (Persero)", "Transportasi", "Kota Bandung", "Dedi Sukmana", "mentor.dedi@kai.id")
        };

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

            var mentorUser = await CreateDemoUserAsync(userManager, null, mentorEmail, mentorName, UserRole.IndustryMentor, ct);
            mentors.Add(mentorUser);
        }
        db.Companies.AddRange(companies);
        await db.SaveChangesAsync(ct);

        // 4. Seed School Tenants (Realistic Indonesian SMK)
        var tenantProfiles = new[]
        {
            (Name: "SMK Negeri 1 Jakarta", Npsn: "20101001", City: "Kota Jakarta Pusat", Region: "DKI Jakarta", AdminEmail: "mastergemerz2008@gmail.com", DeptHeadEmail: "head.tkj@smkn1jakarta.sch.id", TeacherEmail: "masteralvano@gmail.com", PlanId: planPro.Id),
            (Name: "SMK Negeri 2 Bandung", Npsn: "20202002", City: "Kota Bandung", Region: "Jawa Barat", AdminEmail: "admin@smkn2bandung.sch.id", DeptHeadEmail: "head.rpl@smkn2bandung.sch.id", TeacherEmail: "guru.dewi@gmail.com", PlanId: planPro.Id),
            (Name: "SMK Negeri 5 Surabaya", Npsn: "20303003", City: "Kota Surabaya", Region: "Jawa Timur", AdminEmail: "admin@smkn5surabaya.sch.id", DeptHeadEmail: "head.dkv@smkn5surabaya.sch.id", TeacherEmail: "guru.fajar@gmail.com", PlanId: planStarter.Id)
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodStart = today.AddDays(-opt.Days);
        var totalPlacements = 0;
        var totalJournalEntries = 0;

        var indonesianStudents = new[]
        {
            ("Ivano Wisnu Budi Laras", "ivanowisnubudilaras2008@gmail.com"),
            ("Ahmad Rizky Pratama", "ahmad.rizky@gmail.com"),
            ("Budi Santoso", "budi.santoso@gmail.com"),
            ("Siti Nurhaliza", "siti.nurhaliza@gmail.com"),
            ("Dewi Maharani", "dewi.maharani@gmail.com"),
            ("Fajar Hidayat", "fajar.hidayat@gmail.com"),
            ("Rizky Ramadhan", "rizky.ramadhan@gmail.com"),
            ("Nabila Putri Pratama", "nabila.putri@gmail.com"),
            ("Dimas Anggara", "dimas.anggara@gmail.com"),
            ("Eka Prasetya", "eka.prasetya@gmail.com"),
            ("Fitriani Rahmawati", "fitriani.rahma@gmail.com"),
            ("Hendra Wijaya", "hendra.wijaya@gmail.com"),
            ("Indah Permatasari", "indah.permatasari@gmail.com"),
            ("Joko Susilo", "joko.susilo@gmail.com"),
            ("Kurniawan Dwi Saputra", "kurniawan.dwi@gmail.com"),
            ("Lestari Anggraini", "lestari.anggraini@gmail.com"),
            ("Muhammad Iqbal", "muhammad.iqbal@gmail.com"),
            ("Nurul Hidayah", "nurul.hidayah@gmail.com"),
            ("Oki Setiawan", "oki.setiawan@gmail.com"),
            ("Putri Utami", "putri.utami@gmail.com"),
            ("Rahmat Hidayat", "rahmat.hidayat@gmail.com"),
            ("Sari Indah", "sari.indah@gmail.com"),
            ("Taufik Hidayat", "taufik.hidayat@gmail.com"),
            ("Utami Dewi", "utami.dewi@gmail.com"),
            ("Vina Panduwinata", "vina.pandu@gmail.com"),
            ("Wahyu Setiawan", "wahyu.setiawan@gmail.com"),
            ("Yulia Rahma", "yulia.rahma@gmail.com"),
            ("Zainal Abidin", "zainal.abidin@gmail.com"),
            ("Aditya Pratama", "aditya.pratama@gmail.com"),
            ("Bayu Skak", "bayu.skak@gmail.com"),
            ("Cinta Laura", "cinta.laura@gmail.com")
        };

        var faker = new Faker("id_ID");

        foreach (var profile in tenantProfiles)
        {
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
                var (stName, stEmail) = indonesianStudents[i];
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
                students.Add(student);

                await CreateDemoUserAsync(userManager, tenant.Id, stEmail, stName, UserRole.Student, ct);
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
                    if (faker.Random.Double() > 0.05)
                    {
                        entry = new JournalEntry
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenant.Id,
                            SlotId = slot.Id,
                            PlacementId = placement.Id,
                            Text = $"Mengikuti kegiatan pengerjaan tugas harian di {company.Name}. Melakukan pemeliharaan jaringan dan dokumentasi.",
                            Status = JournalEntryStatus.Approved,
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

        return $"RESET & SEED REAL SUCCESSFUL: 2 SuperAdmin (`superadmin@vokasia.id`, `superadmin@gmail.com`), {tenantProfiles.Length} Sekolah (SMKN 1 JKT, SMKN 2 BDG, SMKN 5 SBY), {totalPlacements} Siswa Indonesia (@gmail.com), {realCompanies.Length} DUDI (Telkom, Astra, BCA, Indosat, Tokopedia, PLN), {totalJournalEntries} Jurnal Terisi.";
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

