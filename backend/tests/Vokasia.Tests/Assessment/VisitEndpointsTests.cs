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

/// <summary>
/// VOK-H5-E1 §1 — kunjungan guru (CreateVisit/ListVisits, policy Teacher+).
///
/// [CAKUPAN, dicatat eksplisit]: Suite ini TIDAK menguji jalur `SignatureDataUrl` terisi (yang
/// memicu `IMinioClient.PutObjectAsync` — koneksi MinIO SUNGGUHAN, bukan cuma presign-URL murni
/// spt `GetPresignedUploadUrl` yang sudah dites JournalStudentEndpointsTests). VokasiaApiFactory
/// (Auth/) memakai EF InMemory tanpa docker-compose hidup (dicek: `docker ps` kosong saat sesi
/// ini) — pola SAMA dgn seluruh suite Journal lain yang juga hanya menguji presign, bukan upload
/// nyata. Jalur upload signature PNG akan tercakup H5-E3 (integration test Testcontainers penuh).
/// </summary>
public class VisitEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public VisitEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedTeacherClientAsync(Guid tenantId, string emailPrefix = "visit-teacher")
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, emailPrefix, UserRole.Teacher, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task<Guid> SeedPlacementAsync(Guid tenantId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Kunjungan", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Kunjungan" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Dikunjungi", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();
        return placement.Id;
    }

    [Fact]
    public async Task CreateVisit_ValidRequest_PersistsAndReturnsCreated()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId);
        var placementId = await SeedPlacementAsync(tenantId);

        var resp = await client.PostAsJsonAsync($"/api/placements/{placementId}/visits", new
        {
            Date = new DateOnly(2026, 7, 15), Notes = "Kunjungan bulan Juli, siswa aktif.", PhotoKey = (string?)null, SignatureDataUrl = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(placementId, body.GetProperty("placementId").GetGuid());
        Assert.Equal("Kunjungan bulan Juli, siswa aktif.", body.GetProperty("notes").GetString());
        Assert.True(body.GetProperty("signatureKey").ValueKind is JsonValueKind.Null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.Entity == nameof(Visit) && a.Action == "VisitCreated");
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task CreateVisit_PlacementNotFound_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId, "visit-404");

        var resp = await client.PostAsJsonAsync($"/api/placements/{Guid.NewGuid()}/visits", new
        {
            Date = new DateOnly(2026, 7, 15), Notes = "Kunjungan ke placement tak ada.", PhotoKey = (string?)null, SignatureDataUrl = (string?)null,
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ListVisits_ReturnsNewestDateFirst()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId, "visit-list");
        var placementId = await SeedPlacementAsync(tenantId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            db.Visits.AddRange(
                new Visit { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, TeacherId = Guid.NewGuid(), Date = new DateOnly(2026, 6, 1), Notes = "Kunjungan pertama" },
                new Visit { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, TeacherId = Guid.NewGuid(), Date = new DateOnly(2026, 7, 1), Notes = "Kunjungan kedua" });
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/placements/{placementId}/visits");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetArrayLength());
        Assert.Equal("Kunjungan kedua", body[0].GetProperty("notes").GetString());
        Assert.Equal("Kunjungan pertama", body[1].GetProperty("notes").GetString());
    }

    [Fact]
    public async Task ListVisits_PlacementNotFound_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId, "visit-list-404");

        var resp = await client.GetAsync($"/api/placements/{Guid.NewGuid()}/visits");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task CreateVisit_StudentRole_Forbidden()
    {
        var tenantId = Guid.NewGuid();
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "visit-student", UserRole.Student, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var placementId = await SeedPlacementAsync(tenantId);

        var resp = await client.PostAsJsonAsync($"/api/placements/{placementId}/visits", new
        {
            Date = new DateOnly(2026, 7, 15), Notes = "Siswa coba catat kunjungan sendiri.", PhotoKey = (string?)null, SignatureDataUrl = (string?)null,
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
