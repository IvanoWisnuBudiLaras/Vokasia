using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H6-E1 §6 — Portfolio publik siswa. Prioritas #1 ticket bersama Tenants (gate M5): "portfolio
/// publik + tenant provisioning". AC literal: kompetensi terverifikasi dari proyeksi jurnal Approved,
/// kurasi hanya jurnal Approved milik sendiri, publish->unpublish->404, payload publik tanpa NISN/kontak.
/// </summary>
[Collection("IntegrationTests")]
public class PortfolioTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public PortfolioTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private sealed record Fixture(Guid StudentId, Guid ApprovedJournalId, Guid ForeignJournalId, string CompetencyName);

    private async Task<Fixture> SeedApprovedJournalAndCertificateAsync(Guid tenantId, Guid studentUserId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var major = new Major { Id = Guid.NewGuid(), TenantId = tenantId, Name = "TKJ" };
        var competency = new Competency { Id = Guid.NewGuid(), TenantId = tenantId, MajorId = major.Id, Name = "Instalasi Jaringan" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = studentUserId, FullName = "Siswa Portofolio", MajorId = major.Id, Classroom = "XII A" };
        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Portofolio", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 6, 30), ClassLevels = "XII", Status = PeriodStatus.Closed };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Portofolio" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Completed };

        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = new DateOnly(2026, 3, 1), Status = JournalSlotStatus.Filled };
        var approvedEntry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slot.Id, PlacementId = placement.Id, Text = "Belajar instalasi jaringan LAN.", Status = JournalEntryStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow };
        var journalCompetency = new JournalCompetency { JournalEntryId = approvedEntry.Id, CompetencyId = competency.Id };

        // Placement LAIN (bukan milik siswa ini) - jurnal ini dipakai test menolak kurasi lintas-siswa.
        var otherStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Lain", MajorId = major.Id, Classroom = "XII B" };
        var otherPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = otherStudent.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Completed };
        var otherSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = otherPlacement.Id, Date = new DateOnly(2026, 3, 1), Status = JournalSlotStatus.Filled };
        var foreignEntry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = otherSlot.Id, PlacementId = otherPlacement.Id, Text = "Jurnal siswa lain.", Status = JournalEntryStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow };

        var certificate = new Certificate { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, CertCode = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(), PdfKey = "dummy.pdf" };

        db.Majors.Add(major);
        db.Competencies.Add(competency);
        db.Students.Add(student);
        db.Students.Add(otherStudent);
        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Placements.Add(placement);
        db.Placements.Add(otherPlacement);
        db.JournalSlots.Add(slot);
        db.JournalSlots.Add(otherSlot);
        db.JournalEntries.Add(approvedEntry);
        db.JournalEntries.Add(foreignEntry);
        db.JournalCompetencies.Add(journalCompetency);
        db.Certificates.Add(certificate);
        await db.SaveChangesAsync();

        return new Fixture(student.Id, approvedEntry.Id, foreignEntry.Id, competency.Name);
    }

    [Fact]
    public async Task PublishThenUnpublish_PublicEndpointGoes200ThenNotFound_WithoutSensitiveFields()
    {
        var tenant = await _factory.SeedTenantAsync("SMK Portofolio Uji");
        var (student, studentClient) = await _factory.LoginAsAsync(UserRole.Student, tenant.Id, "portfolio-publish");
        var fx = await SeedApprovedJournalAndCertificateAsync(tenant.Id, student.Id);

        // GetMyPortfolio: kompetensi terverifikasi + sertifikat sudah terbaca dari proyeksi H4/H5.
        var myPortfolioResp = await studentClient.GetAsync("/api/portfolio");
        Assert.Equal(HttpStatusCode.OK, myPortfolioResp.StatusCode);
        var myPortfolio = await myPortfolioResp.Content.ReadFromJsonAsync<JsonElement>();
        var competencies = myPortfolio.GetProperty("verifiedCompetencies").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains(fx.CompetencyName, competencies);
        Assert.True(myPortfolio.GetProperty("certificate").ValueKind != JsonValueKind.Null);

        // UpdatePortfolio: kurasi 1 sampel Approved milik sendiri.
        var updateResp = await studentClient.PutAsJsonAsync("/api/portfolio", new { Headline = "Siap kerja di bidang jaringan", SampleJournalIds = new[] { fx.ApprovedJournalId } });
        Assert.Equal(HttpStatusCode.NoContent, updateResp.StatusCode);

        // PublishPortfolio -> slug.
        var publishResp = await studentClient.PostAsync("/api/portfolio/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);
        var publishBody = await publishResp.Content.ReadFromJsonAsync<JsonElement>();
        var slug = publishBody.GetProperty("slug").GetString()!;
        Assert.Contains("tkj", slug, StringComparison.OrdinalIgnoreCase);

        // GetPublicPortfolio (ANONIM) -> 200, tanpa NISN/kontak (dibuktikan lewat daftar properti mentah, pola sama VerifyCertificate_ValidCode_Returns200WithoutSensitiveFields).
        var anon = _factory.CreateClient();
        var publicResp = await anon.GetAsync($"/p/{slug}");
        Assert.Equal(HttpStatusCode.OK, publicResp.StatusCode);
        Assert.Equal("public, max-age=300", publicResp.Headers.CacheControl?.ToString());

        var publicBody = await publicResp.Content.ReadFromJsonAsync<JsonElement>();
        var propertyNames = publicBody.EnumerateObject().Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.DoesNotContain(propertyNames, p => p.Contains("nisn") || p.Contains("kontak") || p.Contains("contact") || p.Contains("phone") || p.Contains("email"));
        Assert.Equal("Siswa Portofolio", publicBody.GetProperty("studentName").GetString());
        Assert.Equal("SMK Portofolio Uji", publicBody.GetProperty("schoolName").GetString());
        Assert.True(publicBody.GetProperty("hasCertificate").GetBoolean());
        Assert.Contains(fx.CompetencyName, publicBody.GetProperty("verifiedCompetencies").EnumerateArray().Select(e => e.GetString()));

        // UnpublishPortfolio -> publik 404 (AC literal).
        var unpublishResp = await studentClient.PostAsync("/api/portfolio/unpublish", null);
        Assert.Equal(HttpStatusCode.NoContent, unpublishResp.StatusCode);

        var afterUnpublishResp = await anon.GetAsync($"/p/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, afterUnpublishResp.StatusCode);
    }

    [Fact]
    public async Task UpdatePortfolio_ForeignOrNonApprovedJournal_RejectsWithValidationProblem()
    {
        var tenant = await _factory.SeedTenantAsync("SMK Portofolio Tolak");
        var (student, studentClient) = await _factory.LoginAsAsync(UserRole.Student, tenant.Id, "portfolio-reject");
        var fx = await SeedApprovedJournalAndCertificateAsync(tenant.Id, student.Id);

        // Jurnal APPROVED tapi milik siswa LAIN - harus ditolak (AC: "hanya jurnal Approved milik sendiri").
        var resp = await studentClient.PutAsJsonAsync("/api/portfolio", new { Headline = (string?)null, SampleJournalIds = new[] { fx.ForeignJournalId } });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
