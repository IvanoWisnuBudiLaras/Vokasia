using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H6-E1 §1 — CreateTenant wizard (prioritas #1 ticket, gate M5): POST /sa/tenants (HTTP,
/// SaOnly) -> Tenant+RubricTemplate default+AppUser TenantAdmin dlm SATU transaksi (BeginTransactionAsync,
/// SaTenantsEndpoints.CreateTenant) -> OutboxMessage{TenantAdminInvited} -> OutboxDispatcher (poll 2 dtk,
/// Worker sungguhan) -> TenantAdminInvitedConsumer (Worker sungguhan) -> IdempotentEmailSender ->
/// baris SentEmail. AC literal ticket: "admin baru bisa login & rubrik default ada (bukti gate M5)".
/// </summary>
[Collection("IntegrationTests")]
public class SaTenantProvisioningTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public SaTenantProvisioningTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private async Task<Guid> SeedPlanAsync(HttpClient saClient, string name = "Paket Uji")
    {
        var resp = await saClient.PostAsJsonAsync("/sa/plans", new { Name = name, PriceMonthly = 500000m, MaxStudents = 100, MaxPlacements = 50 });
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task CreateTenant_ValidWizard_SeedsDefaultRubricAndAdminCanLogin()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-wizard");
        var planId = await SeedPlanAsync(saClient);

        var adminEmail = $"tenantadmin-{Guid.NewGuid():N}@vokasia.test";
        var createResp = await saClient.PostAsJsonAsync("/sa/tenants", new
        {
            SchoolName = "SMK Wizard Uji",
            Npsn = "12345678",
            City = "Bandung",
            AdminEmail = adminEmail,
            AdminName = "Admin Wizard Uji",
            PlanId = planId,
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var tenantDto = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenantDto.GetProperty("id").GetGuid();
        Assert.Equal("SMK Wizard Uji", tenantDto.GetProperty("schoolName").GetString());
        Assert.True(tenantDto.GetProperty("isActive").GetBoolean());

        // AC: "rubrik default ada" — bobot Σ=100, 3 aspek (Teknis/Softskill/Kehadiran).
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var rubric = await db.RubricTemplates.AsNoTracking().Include(r => r.Aspects)
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.IsDefault);
            Assert.NotNull(rubric);
            Assert.Equal(3, rubric!.Aspects.Count);
            Assert.Equal(100, rubric.Aspects.Sum(a => a.Weight));
        }

        // Ambil TempPassword dari OutboxMessage{TenantAdminInvited} (satu-satunya tempat tersedia -
        // endpoint TIDAK PERNAH mengembalikannya di response, by design keamanan).
        string tempPassword = null!;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var outbox = await db.OutboxMessages.AsNoTracking()
                .Where(m => m.Type == "TenantAdminInvited" && m.PayloadJson.Contains(adminEmail))
                .FirstOrDefaultAsync();
            Assert.NotNull(outbox);
            var payload = JsonSerializer.Deserialize<JsonElement>(outbox!.PayloadJson);
            tempPassword = payload.GetProperty("TempPassword").GetString()!;
        }
        Assert.False(string.IsNullOrWhiteSpace(tempPassword));

        // AC: "admin baru bisa login" — POST /account/login LANGSUNG (bukan dance code+PKCE penuh,
        // cukup buktikan password sementara sungguhan valid): 303 SeeOther ke returnUrl (BUKAN
        // redirect balik ke /account/login?error= yang jadi perilaku pada password salah).
        var anon = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginResp = await anon.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["email"] = adminEmail,
            ["password"] = tempPassword,
            ["returnUrl"] = "/",
        }));
        Assert.Equal(HttpStatusCode.SeeOther, loginResp.StatusCode);
        Assert.DoesNotContain("account/login", loginResp.Headers.Location!.ToString());

        // Email undangan sungguhan terkirim (TenantAdminInvitedConsumer + IdempotentEmailSender -> SentEmail).
        await PollUntil.SucceedsAsync(async () =>
        {
            using var scope = _factory.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var sent = await db.SentEmails.AsNoTracking().AnyAsync(e => e.ToEmail == adminEmail && e.TemplateId == "TenantAdminInvite");
            Assert.True(sent);
        }, timeout: TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task CreateTenant_DuplicateAdminEmail_ReturnsValidationProblemAndNoOrphanTenant()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-dup");
        var planId = await SeedPlanAsync(saClient, "Paket Dup");
        var adminEmail = $"dup-admin-{Guid.NewGuid():N}@vokasia.test";

        var first = await saClient.PostAsJsonAsync("/sa/tenants", new
        {
            SchoolName = "SMK Dup Pertama",
            Npsn = (string?)null,
            City = "Jakarta",
            AdminEmail = adminEmail,
            AdminName = "Admin Dup",
            PlanId = planId,
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Dua kali dgn email admin SAMA -> harus ditolak (email unik, RequireUniqueEmail=true) TANPA
        // meninggalkan baris Tenant/RubricTemplate "yatim" dari percobaan kedua (satu transaksi -
        // BUT di percobaan KEDUA ini, FindByEmailAsync mendeteksi duplikat SEBELUM transaksi dibuka
        // sama sekali, jadi tak ada Tenant kedua ditulis - dibuktikan lewat count di bawah).
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var countBefore = await db.Tenants.AsNoTracking().CountAsync(t => t.SchoolName == "SMK Dup Kedua");

        var second = await saClient.PostAsJsonAsync("/sa/tenants", new
        {
            SchoolName = "SMK Dup Kedua",
            Npsn = (string?)null,
            City = "Jakarta",
            AdminEmail = adminEmail,
            AdminName = "Admin Dup Lain",
            PlanId = planId,
        });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);

        var countAfter = await db.Tenants.AsNoTracking().CountAsync(t => t.SchoolName == "SMK Dup Kedua");
        Assert.Equal(countBefore, countAfter); // tetap nol - tak ada Tenant "yatim" tertinggal.
    }

    [Fact]
    public async Task DeactivateTenant_BlocksNewPlacementCreationButKeepsDataReadable()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-deactivate");
        var tenant = await _factory.SeedTenantAsync("SMK Nonaktif Uji");
        var (_, adminClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenant.Id, "deactivate-admin");

        Guid periodId, studentId, companyId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Periode Nonaktif", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenant.Id, FullName = "Siswa Nonaktif", MajorId = Guid.NewGuid(), Classroom = "XII A" };
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Nonaktif" };
            db.Periods.Add(period);
            db.Students.Add(student);
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            periodId = period.Id;
            studentId = student.Id;
            companyId = company.Id;
        }

        // Sebelum nonaktif: CreatePlacement normal harus 201 (data lama tetap terbaca via ListPlacements jg).
        var beforeResp = await adminClient.PostAsJsonAsync("/api/placements", new { StudentId = studentId, CompanyId = companyId, PeriodId = periodId, TeacherId = Guid.NewGuid(), MentorEmail = (string?)null });
        Assert.Equal(HttpStatusCode.Created, beforeResp.StatusCode);

        var listBeforeResp = await adminClient.GetAsync($"/api/placements?periodId={periodId}");
        Assert.Equal(HttpStatusCode.OK, listBeforeResp.StatusCode);

        var deactivateResp = await saClient.PostAsJsonAsync($"/sa/tenants/{tenant.Id}/deactivate", new { Reason = "Uji nonaktif H6-E1" });
        Assert.Equal(HttpStatusCode.NoContent, deactivateResp.StatusCode);

        // AC: "placement baru terblokir" — CreatePlacement (student BARU, hindari unique-constraint noise) harus ditolak.
        Guid studentId2;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var student2 = new Student { Id = Guid.NewGuid(), TenantId = tenant.Id, FullName = "Siswa Nonaktif 2", MajorId = Guid.NewGuid(), Classroom = "XII B" };
            db.Students.Add(student2);
            await db.SaveChangesAsync();
            studentId2 = student2.Id;
        }
        var afterResp = await adminClient.PostAsJsonAsync("/api/placements", new { StudentId = studentId2, CompanyId = companyId, PeriodId = periodId, TeacherId = Guid.NewGuid(), MentorEmail = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, afterResp.StatusCode);

        // AC: "data TIDAK dihapus" — ListPlacements (data lama) tetap 200 setelah nonaktif.
        var listAfterResp = await adminClient.GetAsync($"/api/placements?periodId={periodId}");
        Assert.Equal(HttpStatusCode.OK, listAfterResp.StatusCode);
        var listAfterBody = await listAfterResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(listAfterBody.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task GetTenant_ReturnsStatsMatchingSeededData()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-stats");
        var tenant = await _factory.SeedTenantAsync("SMK Stats Uji");

        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            db.Students.Add(new Student { Id = Guid.NewGuid(), TenantId = tenant.Id, FullName = "Siswa Stats 1", MajorId = Guid.NewGuid(), Classroom = "XII A" });
            db.Students.Add(new Student { Id = Guid.NewGuid(), TenantId = tenant.Id, FullName = "Siswa Stats 2", MajorId = Guid.NewGuid(), Classroom = "XII A" });
            await db.SaveChangesAsync();
        }

        var resp = await saClient.GetAsync($"/sa/tenants/{tenant.Id}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("stats").GetProperty("studentCount").GetInt32());
        Assert.Equal(0, body.GetProperty("stats").GetProperty("activePlacementCount").GetInt32());
    }
}
