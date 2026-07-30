using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Journal;

/// <summary>
/// AC VOK-H4-E1 §4 GetSchoolDashboard (W3): journalTodayPct (div-by-zero -> 0.0, bukan NaN),
/// pendingApprovals count, daftar flagged (Rag!=Green) + teks alasan per level. Dites lewat HTTP
/// nyata (Bearer JWT sungguhan, AuthTestHelpers - pola sama RbacPolicyTests) supaya RBAC gate
/// (RbacPolicies.TenantMember) DAN proyeksi data ikut terbukti sekaligus. Gate RBAC (peran mana
/// lolos/ditolak) SUDAH dites RbacPolicyTests.cs - fokus suite INI pada KEBENARAN ISI respons.
///
/// ReadFromJsonAsync&lt;JsonElement&gt; (bukan deserialisasi ke SchoolDashboardDto langsung) - pola
/// SAMA persis dgn JournalStudentEndpointsTests.cs/JournalMentorEndpointsTests.cs di suite ini;
/// enum diserialisasi sbg ANGKA (tak ada JsonStringEnumConverter global), dibandingkan sbg int.
/// </summary>
public class DashboardEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public DashboardEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedTeacherClientAsync(Guid tenantId)
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "teacher-dash", UserRole.Teacher, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static readonly DateOnly Today = AppTimeZone.TodayJakarta();

    [Fact]
    public async Task GetSchoolDashboard_ComputesPercentagePendingApprovalsAndFlaggedList()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId);

        Guid periodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Dashboard", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Uji Dashboard" };

            // 2 placement aktif dlm periode ini -> 1 slot Filled hari ini, 1 slot Empty hari ini -> 50%.
            var studentFilled = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Sudah Isi", MajorId = Guid.NewGuid(), Classroom = "XII A" };
            var placementFilled = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentFilled.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var slotFilled = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementFilled.Id, Date = Today, Status = JournalSlotStatus.Filled };

            var studentEmpty = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Belum Isi", MajorId = Guid.NewGuid(), Classroom = "XII B" };
            var placementEmpty = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentEmpty.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var slotEmpty = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementEmpty.Id, Date = Today, Status = JournalSlotStatus.Empty };

            // 1 entry Submitted (pending approval) - slot sendiri, tak harus terkait 2 slot di atas.
            var pendingSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementFilled.Id, Date = Today.AddDays(-1), Status = JournalSlotStatus.Filled };
            var pendingEntry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = pendingSlot.Id, PlacementId = placementFilled.Id, Text = "Menunggu approval", Status = JournalEntryStatus.Submitted };

            // 1 siswa flagged Red hari ini.
            var flaggedStatus = new StudentDailyStatus { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentEmpty.Id, PeriodId = periodId, Date = Today, Rag = RagStatus.Red };

            db.Periods.Add(period);
            db.Companies.Add(company);
            db.Students.AddRange(studentFilled, studentEmpty);
            db.Placements.AddRange(placementFilled, placementEmpty);
            db.JournalSlots.AddRange(slotFilled, slotEmpty, pendingSlot);
            db.JournalEntries.Add(pendingEntry);
            db.StudentDailyStatuses.Add(flaggedStatus);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/dashboard/school/{periodId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(50.0, body.GetProperty("journalTodayPct").GetDouble());
        Assert.Equal(1, body.GetProperty("pendingApprovals").GetInt32());
        var flagged = body.GetProperty("flagged");
        Assert.Equal(1, flagged.GetArrayLength());
        var flaggedItem = flagged[0];
        Assert.Equal((int)RagStatus.Red, flaggedItem.GetProperty("rag").GetInt32());
        Assert.Equal("PT Uji Dashboard", flaggedItem.GetProperty("companyName").GetString());
        Assert.Equal("≥ 3 hari kerja tanpa jurnal", flaggedItem.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetSchoolDashboard_NoSlotsToday_PercentageIsZeroNotNaN()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId);

        Guid periodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Kosong", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            db.Periods.Add(period);
            // TIDAK ada placement/slot sama sekali utk periode ini - kasus akhir pekan/libur/cron
            // 05:00 belum jalan (lihat komentar endpoint asli).
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/dashboard/school/{periodId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.0, body.GetProperty("journalTodayPct").GetDouble()); // 0/0 -> 0.0, bukan NaN/exception.
        Assert.Equal(0, body.GetProperty("pendingApprovals").GetInt32());
        Assert.Equal(0, body.GetProperty("flagged").GetArrayLength());
    }

    [Fact]
    public async Task GetSchoolDashboard_FlaggedList_ExcludesGreenIncludesAmberWithCorrectReason()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId);

        Guid periodId;
        Guid amberStudentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Amber", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Amber" };

            var greenStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Hijau", MajorId = Guid.NewGuid(), Classroom = "XII C" };
            var greenPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = greenStudent.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var greenStatus = new StudentDailyStatus { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = greenStudent.Id, PeriodId = periodId, Date = Today, Rag = RagStatus.Green };

            var amberStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Amber", MajorId = Guid.NewGuid(), Classroom = "XII D" };
            amberStudentId = amberStudent.Id;
            var amberPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = amberStudent.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var amberStatus = new StudentDailyStatus { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = amberStudent.Id, PeriodId = periodId, Date = Today, Rag = RagStatus.Amber };

            db.Periods.Add(period);
            db.Companies.Add(company);
            db.Students.AddRange(greenStudent, amberStudent);
            db.Placements.AddRange(greenPlacement, amberPlacement);
            db.StudentDailyStatuses.AddRange(greenStatus, amberStatus);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/dashboard/school/{periodId}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var flagged = body.GetProperty("flagged");
        Assert.Equal(1, flagged.GetArrayLength()); // Green TIDAK muncul - hanya Amber.
        var flaggedItem = flagged[0];
        Assert.Equal(amberStudentId, flaggedItem.GetProperty("studentId").GetGuid());
        Assert.Equal((int)RagStatus.Amber, flaggedItem.GetProperty("rag").GetInt32());
        Assert.Equal("1-2 hari kerja tanpa jurnal", flaggedItem.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task GetSchoolDashboard_ScopesToRequestedPeriodOnly_OtherPeriodDataNotLeaked()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId);

        Guid periodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode A", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;

            var otherPeriod = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Lain", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Periode Lain" };
            var otherStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Periode Lain", MajorId = Guid.NewGuid(), Classroom = "XII E" };
            var otherPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = otherStudent.Id, CompanyId = company.Id, PeriodId = otherPeriod.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var otherSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = otherPlacement.Id, Date = Today, Status = JournalSlotStatus.Empty };
            var otherStatus = new StudentDailyStatus { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = otherStudent.Id, PeriodId = otherPeriod.Id, Date = Today, Rag = RagStatus.Red };

            db.Periods.AddRange(period, otherPeriod);
            db.Companies.Add(company);
            db.Students.Add(otherStudent);
            db.Placements.Add(otherPlacement);
            db.JournalSlots.Add(otherSlot);
            db.StudentDailyStatuses.Add(otherStatus);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/dashboard/school/{periodId}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // periodId (Periode A) TIDAK punya placement/slot/status sama sekali - data "Periode Lain"
        // TIDAK BOLEH bocor ke sini.
        Assert.Equal(0.0, body.GetProperty("journalTodayPct").GetDouble());
        Assert.Equal(0, body.GetProperty("flagged").GetArrayLength());
    }
}
