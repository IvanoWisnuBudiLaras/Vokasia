using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 CertificateFlowTests — assessment final → EnqueueCertificateBatch (cron, DIPICU
/// MANUAL via <see cref="VokasiaIntegrationFactory.TriggerEnqueueCertificateBatchAsync"/>, sesuai
/// AC ticket) → OutboxMessage CertificateRequested → CertificateGeneratorConsumer (Worker
/// sungguhan) → PDF tersimpan MinIO (bucket compose dev, lihat doc-comment VokasiaIntegrationFactory)
/// + baris Certificate → VerifyCertificate publik 200 TANPA field sensitif (NISN/kontak/nilai -
/// diverifikasi dgn membaca daftar properti JSON mentah, bukan cuma cek field yang diharapkan ADA) →
/// CertCode palsu → 404.
/// </summary>
[Collection("IntegrationTests")]
public class CertificateFlowTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public CertificateFlowTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private async Task<(Guid PlacementId, DateOnly FinalizedDate, string SchoolName)> SeedFinalizedPlacementAsync(Guid tenantId, string schoolName)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var rubric = new RubricTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rubrik Sertifikat", IsDefault = true, Aspects = [] };
        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Sertifikat", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Closed };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Sertifikat" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Sertifikat", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };

        // "Finalized kemarin" (zona WIB) - persis definisi EnqueueCertificateBatch (lihat doc-comment kelasnya).
        var finalizedYesterdayJakarta = AppTimeZone.TodayJakarta().AddDays(-1);
        var finalizedAtUtc = TimeZoneInfo.ConvertTimeToUtc(finalizedYesterdayJakarta.ToDateTime(new TimeOnly(10, 0)), AppTimeZone.Jakarta);
        var assessment = new Vokasia.Domain.Entities.Assessment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, RubricTemplateId = rubric.Id,
            IsFinal = true, FinalScore = 88.5m, FinalizedAt = new DateTimeOffset(finalizedAtUtc, TimeSpan.Zero),
        };

        db.RubricTemplates.Add(rubric);
        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();

        return (placement.Id, finalizedYesterdayJakarta, schoolName);
    }

    [Fact]
    public async Task FinalizedAssessment_EnqueueCertificateBatch_GeneratesPdfInMinioAndCertificateRow()
    {
        var tenant = await _factory.SeedTenantAsync("SMK Sertifikat Uji");
        var (placementId, finalizedDate, _) = await SeedFinalizedPlacementAsync(tenant.Id, tenant.SchoolName);

        // "cron dipicu manual" (AC ticket) - runDate = finalizedDate+1 supaya EnqueueCertificateBatch
        // menganggap finalizedDate itu "kemarin" (lihat doc-comment AssessmentCronJobs.EnqueueCertificateBatch).
        await _factory.TriggerEnqueueCertificateBatchAsync(finalizedDate.AddDays(1));

        Certificate cert = null!;
        await PollUntil.SucceedsAsync(async () =>
        {
            using var scope = _factory.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var found = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.PlacementId == placementId);
            Assert.NotNull(found);
            cert = found!;
        }, timeout: TimeSpan.FromSeconds(15));

        Assert.Equal(12, cert.CertCode.Length); // CertCodeGenerator (lihat CertCodeGeneratorTests unit) - 12 karakter.

        // PDF SUNGGUHAN ada di MinIO (bukan hanya baris DB) - StatObjectAsync melempar kalau objek tak ada.
        using var apiScope = _factory.Services.CreateScope();
        var minio = apiScope.ServiceProvider.GetRequiredService<IMinioClient>();
        var stat = await minio.StatObjectAsync(new StatObjectArgs().WithBucket("vokasia-journal").WithObject(cert.PdfKey));
        Assert.True(stat.Size > 0);
    }

    [Fact]
    public async Task VerifyCertificate_ValidCode_Returns200WithoutSensitiveFields()
    {
        var tenant = await _factory.SeedTenantAsync("SMK Verifikasi Uji");
        var (placementId, finalizedDate, _) = await SeedFinalizedPlacementAsync(tenant.Id, tenant.SchoolName);
        await _factory.TriggerEnqueueCertificateBatchAsync(finalizedDate.AddDays(1));

        string certCode = null!;
        await PollUntil.SucceedsAsync(async () =>
        {
            using var scope = _factory.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var found = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.PlacementId == placementId);
            Assert.NotNull(found);
            certCode = found!.CertCode;
        }, timeout: TimeSpan.FromSeconds(15));

        var anonClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await anonClient.GetAsync($"/api/verify/{certCode}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var propertyNames = body.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToList();

        // FR-CRT-02: TANPA NISN/kontak/nilai - dibuktikan lewat DAFTAR properti mentah (bukan cuma
        // cek field yang diharapkan ADA), supaya penambahan field sensitif baru di masa depan akan
        // membuat assert ini gagal (bukan diam-diam lolos krn test lama tak pernah menyebutnya).
        Assert.DoesNotContain(propertyNames, p => p.Contains("score") || p.Contains("nisn") || p.Contains("nilai") || p.Contains("email") || p.Contains("phone") || p.Contains("kontak"));
        Assert.Equal(6, propertyNames.Count); // studentName, schoolName, companyName, periodLabel, issuedAt, valid - PERSIS 6, bukan lebih/kurang.
        Assert.True(body.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task VerifyCertificate_FakeCode_Returns404()
    {
        var anonClient = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await anonClient.GetAsync("/api/verify/KODE-PALSU-TIDAK-ADA");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
