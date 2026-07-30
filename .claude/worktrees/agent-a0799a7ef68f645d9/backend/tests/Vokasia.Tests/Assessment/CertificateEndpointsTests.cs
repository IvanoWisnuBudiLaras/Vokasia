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

namespace Vokasia.Tests.Assessment;

/// <summary>VOK-H5-E1 §5 — GetCertificate (unduh, siswa sendiri/admin) + VerifyCertificate (publik, minimal-data).</summary>
public class CertificateEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public CertificateEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid UserId)> AuthClientAsync(UserRole role, Guid? tenantId, string emailPrefix)
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, emailPrefix, role, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (client, user.Id);
    }

    private async Task<(Guid PlacementId, Guid StudentUserId, string CertCode)> SeedCertifiedPlacementAsync(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var tenant = new Tenant { Id = tenantId, SchoolName = "SMKN Uji Sertifikat" };
        var studentUserId = Guid.NewGuid();
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Sertifikat", MajorId = Guid.NewGuid(), Classroom = "XII A", UserId = studentUserId };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Sertifikat Jaya" };
        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Sertifikat", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), ClassLevels = "XII", Status = PeriodStatus.Closed };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Completed };
        var certCode = Vokasia.Domain.Common.CertCodeGenerator.Generate();
        var certificate = new Certificate { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, CertCode = certCode, PdfKey = $"tenant/{tenantId}/certificates/{placement.Id}.pdf" };

        db.Tenants.Add(tenant);
        db.Students.Add(student);
        db.Companies.Add(company);
        db.Periods.Add(period);
        db.Placements.Add(placement);
        db.Certificates.Add(certificate);
        await db.SaveChangesAsync();

        return (placement.Id, studentUserId, certCode);
    }

    [Fact]
    public async Task VerifyCertificate_ValidCode_ReturnsMinimalDataWithoutSensitiveFields()
    {
        var tenantId = Guid.NewGuid();
        var (placementId, _, certCode) = await SeedCertifiedPlacementAsync(tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); // anonim, tanpa token.

        var resp = await client.GetAsync($"/api/verify/{certCode}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Siswa Sertifikat", body.GetProperty("studentName").GetString());
        Assert.Equal("SMKN Uji Sertifikat", body.GetProperty("schoolName").GetString());
        Assert.Equal("PT Sertifikat Jaya", body.GetProperty("companyName").GetString());
        Assert.True(body.GetProperty("valid").GetBoolean());
        // FR-CRT-02: TANPA NISN/kontak/nilai - properti-properti itu tak boleh ada di JSON sama sekali.
        Assert.False(body.TryGetProperty("nisn", out _));
        Assert.False(body.TryGetProperty("finalScore", out _));
        Assert.False(body.TryGetProperty("contact", out _));
    }

    [Fact]
    public async Task VerifyCertificate_InvalidCode_Returns404()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var resp = await client.GetAsync("/api/verify/kodePalsuXX01");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetCertificate_OwnStudent_ReturnsDownloadUrl()
    {
        var tenantId = Guid.NewGuid();
        var (placementId, studentUserId, _) = await SeedCertifiedPlacementAsync(tenantId);

        // Siswa login LANGSUNG dgn Id yang sama dgn Student.UserId yang sudah di-seed (bukan
        // SeedUserAsync generik yang bikin Id baru) - buat AppUser dgn Id itu scr manual.
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>();
            var email = $"cert-owner-{Guid.NewGuid():N}@vokasia.test";
            var appUser = new Vokasia.Infrastructure.Identity.AppUser { Id = studentUserId, UserName = email, Email = email, FullName = "Siswa Sertifikat", Role = UserRole.Student, TenantId = tenantId, IsActive = true };
            var created = await userManager.CreateAsync(appUser, AuthTestHelpers.Password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, email);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var resp = await client.GetAsync($"/api/placements/{placementId}/certificate");

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("downloadUrl").GetString()));
        }
    }

    [Fact]
    public async Task GetCertificate_DifferentStudent_Returns403()
    {
        var tenantId = Guid.NewGuid();
        var (placementId, _, _) = await SeedCertifiedPlacementAsync(tenantId);
        var (client, _) = await AuthClientAsync(UserRole.Student, tenantId, "cert-intruder-student");

        var resp = await client.GetAsync($"/api/placements/{placementId}/certificate");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetCertificate_TenantAdmin_ReturnsDownloadUrl()
    {
        var tenantId = Guid.NewGuid();
        var (placementId, _, _) = await SeedCertifiedPlacementAsync(tenantId);
        var (client, _) = await AuthClientAsync(UserRole.TenantAdmin, tenantId, "cert-admin");

        var resp = await client.GetAsync($"/api/placements/{placementId}/certificate");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task GetCertificate_PlacementWithoutCertificateYet_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var (client, _) = await AuthClientAsync(UserRole.TenantAdmin, tenantId, "cert-nocert-admin");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Belum Sertifikat", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Belum Sertifikat" };
        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Belum Selesai", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
        db.Students.Add(student);
        db.Companies.Add(company);
        db.Periods.Add(period);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();

        var resp = await client.GetAsync($"/api/placements/{placement.Id}/certificate");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
