using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 TenantIsolationTests — endpoint utama (journals, placements, students, assessment,
/// dashboard, audit) diakses lintas tenant → 404/kosong (BUKAN kebocoran data), lewat query filter
/// tenant EF Core sungguhan (Postgres Testcontainers, bukan InMemory - filter global perlu skema
/// relasional nyata utk benar2 teruji). SuperAdmin tanpa X-Acting-Tenant → kosong (tak ada tenant_id
/// claim, TIDAK error). NFR-SEC-04.
/// </summary>
[Collection("IntegrationTests")]
public class TenantIsolationTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public TenantIsolationTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private async Task<(Tenant Tenant, Guid PlacementId, Guid StudentId)> SeedTenantWithPlacementAsync(string schoolName)
    {
        var tenant = await _factory.SeedTenantAsync(schoolName);
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Periode Isolasi", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var company = new Company { Id = Guid.NewGuid(), Name = $"PT {schoolName}" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenant.Id, FullName = $"Siswa {schoolName}", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenant.Id, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();

        return (tenant, placement.Id, student.Id);
    }

    [Fact]
    public async Task Journals_ListForTeacher_OtherTenantPlacement_NotFound()
    {
        var (_, placementIdB, _) = await SeedTenantWithPlacementAsync("Tenant B Journal");
        var (_, teacherAClient) = await _factory.LoginAsAsync(UserRole.Teacher, Guid.NewGuid(), "iso-teacher-a-journal");

        var resp = await teacherAClient.GetAsync($"/api/journals/for-teacher/{placementIdB}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Placements_GetSingle_OtherTenant_NotFound()
    {
        var (_, placementIdB, _) = await SeedTenantWithPlacementAsync("Tenant B Placement");
        var (_, adminAClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, Guid.NewGuid(), "iso-admin-a-placement");

        var resp = await adminAClient.GetAsync($"/api/placements/{placementIdB}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>`periodId` WAJIB di query (ListPlacements, lihat CompaniesAndPlacements.cs) - tenant
    /// A memakai periodId MILIK TENANT B (didapat lewat IgnoreQueryFilters, simulasi ID ditebak/
    /// bocor dari tempat lain) - filter tenant EF global tetap harus membuat hasil KOSONG, bukan
    /// mengembalikan placement tenant B.</summary>
    [Fact]
    public async Task Placements_ListWithOtherTenantPeriodId_ReturnsEmpty()
    {
        var (_, placementIdB, _) = await SeedTenantWithPlacementAsync("Tenant B List Placement");
        Guid periodIdB;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            periodIdB = await db.Placements.IgnoreQueryFilters().Where(p => p.Id == placementIdB).Select(p => p.PeriodId).FirstAsync();
        }

        var (_, adminAClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, Guid.NewGuid(), "iso-admin-a-list-placement");
        var resp = await adminAClient.GetAsync($"/api/placements?periodId={periodIdB}&pageSize=200");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var ids = body.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();

        Assert.DoesNotContain(placementIdB, ids);
        Assert.Empty(ids);
    }

    [Fact]
    public async Task Students_List_ExcludesOtherTenantRows()
    {
        var (tenantA, _, studentIdA) = await SeedTenantWithPlacementAsync("Tenant A List Student");
        var (_, _, studentIdB) = await SeedTenantWithPlacementAsync("Tenant B List Student");
        var (_, adminAClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenantA.Id, "iso-admin-a-list-student");

        var resp = await adminAClient.GetAsync("/api/students?pageSize=200");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var ids = body.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(studentIdA, ids);
        Assert.DoesNotContain(studentIdB, ids);
    }

    [Fact]
    public async Task Assessment_GetForOtherTenantPlacement_NotFound()
    {
        var (_, placementIdB, _) = await SeedTenantWithPlacementAsync("Tenant B Assessment");
        var (_, adminAClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, Guid.NewGuid(), "iso-admin-a-assessment");

        var resp = await adminAClient.GetAsync($"/api/placements/{placementIdB}/assessment");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>
    /// GetSchoolDashboard TIDAK melempar 404 utk periodId tenant lain (endpoint tak melakukan
    /// existence-check periodId eksplisit) - TAPI filter tenant EF Core global pada
    /// db.Placements/JournalSlots/StudentDailyStatuses di DALAM query membuat SEMUA join kosong
    /// (placement asli milik tenant B tak pernah cocok ambient TenantId tenant A) -&gt; hasil 200
    /// dgn angka NOL/kosong, BUKAN data tenant B bocor. Perilaku NYATA diverifikasi di sini (bukan
    /// diasumsikan 404 tanpa baca kode - lihat DashboardEndpoints.GetSchoolDashboard).
    /// </summary>
    [Fact]
    public async Task Dashboard_OtherTenantPeriodId_ReturnsEmptyNotLeaked()
    {
        var (tenantB, placementIdB, studentIdB) = await SeedTenantWithPlacementAsync("Tenant B Dashboard");
        Guid periodIdB;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            periodIdB = await db.Placements.IgnoreQueryFilters().Where(p => p.Id == placementIdB).Select(p => p.PeriodId).FirstAsync();

            // Tandai siswa tenant B "Merah" hari ini - kalau filter tenant BOCOR, admin tenant A akan
            // melihat baris ini di `flagged`.
            db.StudentDailyStatuses.Add(new StudentDailyStatus
            {
                Id = Guid.NewGuid(), TenantId = tenantB.Id, PeriodId = periodIdB, StudentId = studentIdB,
                Date = Vokasia.Domain.Common.AppTimeZone.TodayJakarta(), Rag = RagStatus.Red,
            });
            await db.SaveChangesAsync();
        }

        var (_, adminAClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, Guid.NewGuid(), "iso-admin-a-dashboard");
        var resp = await adminAClient.GetAsync($"/api/dashboard/school/{periodIdB}");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(0, body.GetProperty("pendingApprovals").GetInt32());
        Assert.Empty(body.GetProperty("flagged").EnumerateArray());
    }

    /// <summary>WriteAudit mengambil TenantId dari ambient ITenantContext (klaim token), BUKAN dari body request - tak ada field tenant di WriteAuditLogRequest sama sekali, jadi tak ada cara "menyuntikkan" tenant lain lewat body.</summary>
    [Fact]
    public async Task Audit_WriteAudit_AlwaysScopedToCallersOwnTenant()
    {
        var tenantA = await _factory.SeedTenantAsync("Tenant A Audit");
        var (adminA, adminAClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenantA.Id, "iso-admin-a-audit");

        var resp = await adminAClient.PostAsJsonAsync("/api/audit", new { Action = "TestAction", Entity = "TestEntity", EntityId = Guid.NewGuid().ToString(), MetaJson = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var log = await db.AuditLogs.IgnoreQueryFilters().Where(a => a.ActorUserId == adminA.Id).OrderByDescending(a => a.Id).FirstAsync();
        Assert.Equal(tenantA.Id, log.TenantId);
    }

    /// <summary>SuperAdmin TANPA header X-Acting-Tenant -&gt; tenant_id claim kosong (TenantResolutionMiddleware), endpoint bare-authorize (GetPendingApprovals) tetap bisa dipanggil TAPI hasilnya query "mentor = diri sendiri" -&gt; SuperAdmin bukan mentor siapa pun -&gt; array kosong, BUKAN error/lintas-tenant bocor.</summary>
    [Fact]
    public async Task SuperAdmin_WithoutActingTenant_PendingApprovals_ReturnsEmpty()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "iso-sa-pending");
        var resp = await client.GetAsync("/api/journals/pending");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        Assert.Empty(body!);
    }
}
