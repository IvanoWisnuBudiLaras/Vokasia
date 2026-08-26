using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Infrastructure.Seeding;

/// <summary>
/// Minimal development seed: 1 tenant, 1 superadmin, 1 guru, 1 mentor, 5 siswa.
/// Password semua akun: "Dev123!"
/// </summary>
public static class DevSeeder
{
    private const string DevPassword = "DevPass123!";
    private const string MarkerEmail = "admin@smkcontoh.local";

    public static async Task<string> SeedAsync(
        VokasiaDbContext db,
        UserManager<AppUser> userManager,
        bool forceReset = false,
        CancellationToken ct = default)
    {
        // Idempotency check
        if (!forceReset)
        {
            var exists = await userManager.FindByEmailAsync(MarkerEmail);
            if (exists != null)
                return "SKIP: Dev seed sudah ada. Gunakan --force untuk reset.";
        }
        else
        {
            await ResetAsync(db, userManager, ct);
        }

        // 1. SuperAdmin (global, no tenant)
        var superAdmin = await CreateUserAsync(userManager, null, "superadmin@vokasia.local", "Super Admin", UserRole.SuperAdmin, ct);

        // 2. Plan (untuk tenant)
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = "Dev Plan",
            PriceMonthly = 0,
            PriceAnnual = 0,
            MaxStudents = 100,
            MaxPlacements = 100,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);

        // 3. Tenant (SMK Contoh)
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            SchoolName = "SMK Contoh Dev",
            Npsn = "99999999",
            Address = "Jl. Contoh No. 1",
            City = "Jakarta",
            IsActive = true,
            PlanId = plan.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        // 4. Tenant Staff (peran sekolah)
        var tenantAdmin = await CreateUserAsync(userManager, tenant.Id, MarkerEmail, "Admin Sekolah", UserRole.TenantAdmin, ct);
        var deptHead = await CreateUserAsync(userManager, tenant.Id, "kepala@smkcontoh.local", "Kepala Jurusan", UserRole.DeptHead, ct);
        var teacher = await CreateUserAsync(userManager, tenant.Id, "guru@smkcontoh.local", "Guru Pembimbing", UserRole.Teacher, ct);

        // 5. Major & Competency
        var major = new Major
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Rekayasa Perangkat Lunak"
        };
        db.Majors.Add(major);

        var competencies = new[]
        {
            "Pemrograman Web & Frontend",
            "Pengembangan Backend & API",
            "Manajemen Database & SQL",
            "Pengujian Perangkat Lunak (QA)",
            "DevOps & Deployment",
            "Troubleshooting & Code Review"
        }.Select(name => new Competency
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            MajorId = major.Id,
            Name = name
        }).ToList();
        db.Competencies.AddRange(competencies);
        await db.SaveChangesAsync(ct);
        var competency = competencies[0];

        // 6. Company (DUDI) + Mentor
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "PT Contoh Dev",
            Sector = "Teknologi",
            City = "Jakarta",
            ContactPerson = "Mentor Dev",
            IsVerified = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);

        // Link company ke tenant
        db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.Id, CompanyId = company.Id });
        await db.SaveChangesAsync(ct);

        // Mentor (global user, linked ke company via placement nanti)
        var mentor = await CreateUserAsync(userManager, null, "mentor@dudi.local", "Mentor DUDI", UserRole.IndustryMentor, ct);

        // 7. Period (semester aktif)
        var today = AppTimeZone.TodayJakarta();
        var period = new Period
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Name = "Semester Genap 2025/2026",
            StartDate = today.AddMonths(-3),
            EndDate = today.AddMonths(3),
            ClassLevels = "XII",
            Status = PeriodStatus.Active
        };
        db.Periods.Add(period);
        await db.SaveChangesAsync(ct);

        // 8. 5 Students + Placements
        var students = new List<Student>();
        var placements = new List<Placement>();

        for (int i = 1; i <= 5; i++)
        {
            var studentUser = await CreateUserAsync(userManager, tenant.Id, $"siswa{i}@smkcontoh.local", $"Siswa {i}", UserRole.Student, ct);

            var student = new Student
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = studentUser.Id,
                Nisn = $"20250{i:D3}",
                FullName = $"Siswa {i}",
                MajorId = major.Id,
                Classroom = "XII RPL 1"
            };
            students.Add(student);

            var placement = new Placement
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                StudentId = student.Id,
                CompanyId = company.Id,
                PeriodId = period.Id,
                TeacherId = teacher.Id,
                MentorUserId = mentor.Id,
                Status = PlacementStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            placements.Add(placement);
        }

        db.Students.AddRange(students);
        db.Placements.AddRange(placements);
        await db.SaveChangesAsync(ct);

        var slotList = new List<JournalSlot>();
        var curr = period.StartDate;
        while (curr <= period.EndDate)
        {
            if (curr.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                foreach (var p in placements)
                {
                    slotList.Add(new JournalSlot
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenant.Id,
                        PlacementId = p.Id,
                        Date = curr,
                        Status = JournalSlotStatus.Empty
                    });
                }
            }
            curr = curr.AddDays(1);
        }
        db.JournalSlots.AddRange(slotList);
        await db.SaveChangesAsync(ct);
        // 9. Audit log
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            ActorUserId = superAdmin.Id,
            Action = "DevSeedCompleted",
            Entity = nameof(Tenant),
            EntityId = tenant.Id.ToString(),
            MetaJson = "{\"students\":5,\"teacher\":1,\"mentor\":1,\"tenant\":1}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return $"DEV SEED OK: superadmin@vokasia.local, admin@smkcontoh.local, guru@smkcontoh.local, mentor@dudi.local, siswa1-5@smkcontoh.local | Password: {DevPassword}";
    }

    private static async Task ResetAsync(VokasiaDbContext db, UserManager<AppUser> userManager, CancellationToken ct)
    {
        // Hapus data terkait dev (berdasarkan email admin)
        var adminUser = await userManager.FindByEmailAsync(MarkerEmail);
        if (adminUser != null && adminUser.TenantId.HasValue)
        {
            var tenantId = adminUser.TenantId.Value;

            // Hapus placements & students di tenant ini
            var placements = await db.Placements.Where(p => p.TenantId == tenantId).ToListAsync(ct);
            var studentIds = placements.Select(p => p.StudentId).ToList();
            var students = await db.Students.Where(s => studentIds.Contains(s.Id)).ToListAsync(ct);
            var studentUserIds = students.Select(s => s.UserId).ToList();

            db.Placements.RemoveRange(placements);
            db.Students.RemoveRange(students);

            // Hapus users siswa
            foreach (var uid in studentUserIds)
            {
                if (uid.HasValue)
                {
                    var u = await userManager.FindByIdAsync(uid.Value.ToString());
                    if (u != null) await userManager.DeleteAsync(u);
                }
            }

            // Hapus staff tenant
            var staffEmails = new[] { MarkerEmail, "kepala@smkcontoh.local", "guru@smkcontoh.local" };
            foreach (var email in staffEmails)
            {
                var u = await userManager.FindByEmailAsync(email);
                if (u != null) await userManager.DeleteAsync(u);
            }

            // Hapus company & mentor
            var company = await db.Companies.FirstOrDefaultAsync(c => c.Name == "PT Contoh Dev", ct);
            if (company != null)
            {
                db.TenantCompanies.RemoveRange(db.TenantCompanies.Where(tc => tc.CompanyId == company.Id));
                db.Companies.Remove(company);
            }

            var mentor = await userManager.FindByEmailAsync("mentor@dudi.local");
            if (mentor != null) await userManager.DeleteAsync(mentor);

            // Hapus majors, competencies, periods, tenant
            db.Competencies.RemoveRange(db.Competencies.Where(c => c.TenantId == tenantId));
            db.Majors.RemoveRange(db.Majors.Where(m => m.TenantId == tenantId));
            db.Periods.RemoveRange(db.Periods.Where(p => p.TenantId == tenantId));
            
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant != null) db.Tenants.Remove(tenant);

            // Hapus superadmin jika ada
            var superAdmin = await userManager.FindByEmailAsync("superadmin@vokasia.local");
            if (superAdmin != null) await userManager.DeleteAsync(superAdmin);

            // Hapus plan dev
            var plan = await db.Plans.FirstOrDefaultAsync(p => p.Name == "Dev Plan", ct);
            if (plan != null) db.Plans.Remove(plan);

            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<AppUser> CreateUserAsync(UserManager<AppUser> userManager, Guid? tenantId, string email, string fullName, UserRole role, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null) return existing;

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

        var result = await userManager.CreateAsync(user, DevPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Gagal buat {email}: {string.Join(",", result.Errors.Select(e => e.Description))}");

        return user;
    }
}
