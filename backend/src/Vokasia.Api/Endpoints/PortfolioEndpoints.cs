using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.RateLimiting;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H6-E1 §6 — Portfolio publik siswa (FR-CRT-03). §1-5 dari editor siswa sendiri
/// (StudentSelf); GetPublicPortfolio anonim + rate-limit + cache 5 menit (AC ticket literal).
/// </summary>
public static class PortfolioEndpoints
{
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";
    private const int PresignedExpirySeconds = 15 * 60; // > Cache-Control publik (5 mnt) - tautan tak kadaluarsa selagi masih di-cache.

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portfolio").WithTags("Portfolio")
            .RequireAuthorization(RbacPolicies.StudentSelf)
            .AddEndpointFilter<ValidationFilter>();

        group.MapGet("/", GetMyPortfolio);
        group.MapPut("/", UpdatePortfolio);
        group.MapPost("/publish", PublishPortfolio);
        group.MapPost("/unpublish", UnpublishPortfolio);

        app.MapGet("/api/portfolio/student/{studentId:guid}", GetStudentPortfolioForStaff).RequireAuthorization(RbacPolicies.TenantMember);

        // Publik BY DESIGN (siapa saja bisa lihat portofolio yang di-publish siswa) - rate limit
        // "public" (pola sama VerifyCertificate/MagicLinkEndpoints.Validate).
        app.MapGet("/p/{slug}", GetPublicPortfolio).RequireRateLimiting(VokasiaRateLimiting.PublicPolicy);

        return app;
    }

    private static async Task<Student?> FindCallerStudentAsync(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct) =>
        await db.Students.FirstOrDefaultAsync(s => s.UserId == tenant.UserId, ct);

    /// <summary>Proyeksi kompetensi TERVERIFIKASI = distinct Competency dari JournalEntry Approved milik siswa (H4) — bukan klaim mentah siswa (H3), ini yang dimaksud ticket "kompetensi dari proyeksi jurnal Approved").</summary>
    private static async Task<List<string>> GetVerifiedCompetenciesAsync(VokasiaDbContext db, Guid studentId, CancellationToken ct) =>
        await (
            from p in db.Placements.AsNoTracking()
            where p.StudentId == studentId
            join je in db.JournalEntries.AsNoTracking() on p.Id equals je.PlacementId
            where je.Status == JournalEntryStatus.Approved
            join jc in db.JournalCompetencies.AsNoTracking() on je.Id equals jc.JournalEntryId
            join comp in db.Competencies.AsNoTracking() on jc.CompetencyId equals comp.Id
            select comp.Name
            ).Distinct().OrderBy(n => n).ToListAsync(ct);

    private static async Task<IResult> GetMyPortfolio(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var student = await FindCallerStudentAsync(db, tenant, ct);
        if (student is null)
        {
            return Results.Forbid();
        }

        var portfolio = await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == student.Id, ct);
        var verifiedCompetencies = await GetVerifiedCompetenciesAsync(db, student.Id, ct);

        var sampleIds = ParseSampleIds(portfolio?.SampleJournalIdsCsv);
        var samples = sampleIds.Count == 0
            ? []
            : await db.JournalEntries.AsNoTracking()
                .Where(je => sampleIds.Contains(je.Id))
                .Select(je => new PortfolioJournalSampleDto(je.Id, je.Text, je.SubmittedAt))
                .ToListAsync(ct);

        var certificate = await (
            from p in db.Placements.AsNoTracking()
            where p.StudentId == student.Id
            join cert in db.Certificates.AsNoTracking() on p.Id equals cert.PlacementId
            select new PortfolioCertificateDto(cert.CertCode, cert.IssuedAt)
            ).FirstOrDefaultAsync(ct);

        return Results.Ok(new PortfolioDto(portfolio?.Headline, verifiedCompetencies, samples, certificate, portfolio?.IsPublished ?? false, portfolio?.Slug));
    }

    /// <summary>AC: "kurasi; hanya jurnal Approved milik sendiri" — ditegakkan (bukan dipercaya dari client): filter Approved + PlacementId milik student ini SEBELUM diterima jadi sampel.</summary>
    private static async Task<IResult> UpdatePortfolio(UpdatePortfolioRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var student = await FindCallerStudentAsync(db, tenant, ct);
        if (student is null)
        {
            return Results.Forbid();
        }

        var sampleIds = req.SampleJournalIds ?? [];
        var ownPlacementIds = await db.Placements.AsNoTracking().Where(p => p.StudentId == student.Id).Select(p => p.Id).ToListAsync(ct);
        var validApprovedIds = sampleIds.Count == 0
            ? []
            : await db.JournalEntries.AsNoTracking()
                .Where(je => sampleIds.Contains(je.Id) && je.Status == JournalEntryStatus.Approved && ownPlacementIds.Contains(je.PlacementId))
                .Select(je => je.Id)
                .ToListAsync(ct);

        var rejected = sampleIds.Except(validApprovedIds).ToList();
        if (rejected.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["SampleJournalIds"] = ["Hanya jurnal Approved milik sendiri yang bisa dijadikan sampel: " + string.Join(",", rejected)],
            });
        }

        var portfolio = await db.Portfolios.FirstOrDefaultAsync(p => p.StudentId == student.Id, ct);
        if (portfolio is null)
        {
            portfolio = new Portfolio { Id = Guid.NewGuid(), TenantId = student.TenantId, StudentId = student.Id };
            db.Portfolios.Add(portfolio);
        }

        portfolio.Headline = req.Headline;
        portfolio.SampleJournalIdsCsv = string.Join(",", validApprovedIds);

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>AC: "opt-in publik; validasi payload publik tidak memuat NISN/kontak (assert server-side, NFR-SEC-05); slug nama-jurusan-tahun unik; audit."</summary>
    private static async Task<IResult> PublishPortfolio(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        AssertPublicDtoHasNoSensitiveFields(); // NFR-SEC-05 — defense-in-depth runtime, lihat doc-comment method.

        var student = await FindCallerStudentAsync(db, tenant, ct);
        if (student is null)
        {
            return Results.Forbid();
        }

        var portfolio = await db.Portfolios.FirstOrDefaultAsync(p => p.StudentId == student.Id, ct);
        if (portfolio is null)
        {
            portfolio = new Portfolio { Id = Guid.NewGuid(), TenantId = student.TenantId, StudentId = student.Id };
            db.Portfolios.Add(portfolio);
        }

        if (string.IsNullOrEmpty(portfolio.Slug))
        {
            var major = await db.Majors.AsNoTracking().FirstOrDefaultAsync(m => m.Id == student.MajorId, ct);
            var year = await (
                from p in db.Placements.AsNoTracking()
                where p.StudentId == student.Id
                join per in db.Periods.AsNoTracking() on p.PeriodId equals per.Id
                orderby per.EndDate descending
                select per.EndDate.Year
                ).FirstOrDefaultAsync(ct);
            if (year == 0)
            {
                year = DateTime.UtcNow.Year;
            }

            portfolio.Slug = await GenerateUniqueSlugAsync(db, student.FullName, major?.Name ?? "umum", year, ct);
        }

        portfolio.IsPublished = true;
        await db.SaveChangesAsync(ct);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = student.TenantId,
            ActorUserId = tenant.UserId!.Value,
            Action = "PortfolioPublished",
            Entity = nameof(Portfolio),
            EntityId = portfolio.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { portfolio.Slug }),
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new PublishPortfolioResult(portfolio.Slug!));
    }

    /// <summary>AC: "cabut kapan pun -> publik 404."</summary>
    private static async Task<IResult> UnpublishPortfolio(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var student = await FindCallerStudentAsync(db, tenant, ct);
        if (student is null)
        {
            return Results.Forbid();
        }

        var portfolio = await db.Portfolios.FirstOrDefaultAsync(p => p.StudentId == student.Id, ct);
        if (portfolio is null)
        {
            return Results.NotFound();
        }

        portfolio.IsPublished = false; // Slug DIPERTAHANKAN (bukan dihapus) - publish ulang nanti pakai slug yang sama, bukan slug baru.
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// AC: publik + rate limit + cacheable (Cache-Control 5 mnt). Data W6 SAJA: nama, sekolah,
    /// jurusan, tahun, DUDI, durasi, kompetensi terverifikasi, sampel (thumbnail), status sertifikat
    /// — TANPA kontak/NISN (ditegakkan struktural: query di bawah TIDAK PERNAH select Student.Nisn
    /// ataupun kolom kontak apa pun ke PublicPortfolioDto).
    /// </summary>
    private static async Task<IResult> GetPublicPortfolio(string slug, VokasiaDbContext db, IMinioClient minio, IConfiguration config, HttpContext http, CancellationToken ct)
    {
        var portfolio = await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        if (portfolio is null)
        {
            return Results.NotFound();
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == portfolio.StudentId, ct);
        var tenant = student is null ? null : await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == student.TenantId, ct);
        var major = student is null ? null : await db.Majors.AsNoTracking().FirstOrDefaultAsync(m => m.Id == student.MajorId, ct);
        if (student is null || tenant is null)
        {
            return Results.NotFound();
        }

        var placementInfo = await (
            from p in db.Placements.AsNoTracking()
            where p.StudentId == student.Id
            join c in db.Companies.AsNoTracking() on p.CompanyId equals c.Id
            join per in db.Periods.AsNoTracking() on p.PeriodId equals per.Id
            orderby per.EndDate descending
            select new { CompanyName = c.Name, per.StartDate, per.EndDate }
            ).FirstOrDefaultAsync(ct);

        var verifiedCompetencies = await GetVerifiedCompetenciesAsync(db, student.Id, ct);

        var sampleIds = ParseSampleIds(portfolio.SampleJournalIdsCsv);
        var photoKeys = sampleIds.Count == 0
            ? []
            : await (
                from ph in db.JournalPhotos.AsNoTracking()
                where sampleIds.Contains(ph.JournalEntryId) && ph.Status == PhotoStatus.Processed
                select new { ph.ObjectKey, ph.ThumbKey }
                ).ToListAsync(ct);

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var thumbUrls = new List<string>();
        foreach (var photo in photoKeys)
        {
            var key = photo.ThumbKey ?? photo.ObjectKey;
            if (!ObjectStorageKeyPolicy.IsOwnedKey(key, tenant.Id, "journal"))
            {
                continue;
            }

            thumbUrls.Add(await minio.PresignedGetObjectAsync(new PresignedGetObjectArgs().WithBucket(bucket).WithObject(key).WithExpiry(PresignedExpirySeconds)));
        }

        var hasCertificate = await db.Certificates.AsNoTracking().Join(db.Placements.AsNoTracking(), c => c.PlacementId, p => p.Id, (c, p) => p.StudentId).AnyAsync(sid => sid == student.Id, ct);

        var durationLabel = placementInfo is null
            ? "-"
            : $"{Math.Max(1, (placementInfo.EndDate.ToDateTime(TimeOnly.MinValue) - placementInfo.StartDate.ToDateTime(TimeOnly.MinValue)).Days / 30)} bulan";
        var year = placementInfo?.EndDate.Year ?? DateTime.UtcNow.Year;

        http.Response.Headers.CacheControl = "public, max-age=300"; // AC literal: "cacheable (Cache-Control 5 mnt)".

        return Results.Ok(new PublicPortfolioDto(
            student.FullName, tenant.SchoolName, major?.Name ?? "-", year,
            placementInfo?.CompanyName ?? "-", durationLabel, verifiedCompetencies, thumbUrls, hasCertificate));
    }

    private static List<Guid> ParseSampleIds(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList();

    private static async Task<string> GenerateUniqueSlugAsync(VokasiaDbContext db, string fullName, string majorName, int year, CancellationToken ct)
    {
        var baseSlug = Slugify($"{fullName}-{majorName}-{year}");
        // Keep the readable identity while avoiding predictable sequential enumeration and making
        // simultaneous publishes for students with the same name/year extremely unlikely to
        // collide. The database uniqueness check below remains authoritative.
        var candidate = $"{baseSlug}-{CreateSlugSuffix()}";
        var suffix = 2;
        while (await db.Portfolios.AsNoTracking().AnyAsync(p => p.Slug == candidate, ct))
        {
            candidate = $"{baseSlug}-{CreateSlugSuffix()}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string CreateSlugSuffix()
    {
        const string alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";
        var bytes = RandomNumberGenerator.GetBytes(8);
        var result = new char[8];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = alphabet[bytes[index] % alphabet.Length];
        }

        return new string(result);
    }

    private static string Slugify(string input)
    {
        var lowered = input.ToLowerInvariant();
        var chars = lowered.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("--"))
        {
            collapsed = collapsed.Replace("--", "-");
        }

        return collapsed.Trim('-');
    }

    /// <summary>
    /// NFR-SEC-05 defense-in-depth: PublishPortfolio ticket AC literal minta assert server-side
    /// bahwa payload publik TIDAK memuat NISN/kontak — reflection thd nama properti PublicPortfolioDto
    /// SEKARANG (bukan cuma percaya bentuk record tak pernah berubah) supaya penambahan field
    /// sensitif baru ke DTO itu di masa depan (mis. "Email"/"Nisn"/"Phone") akan bikin PublishPortfolio
    /// GAGAL keras saat itu juga (bukan diam-diam bocor ke publik) - pola sama semangatnya dgn assert
    /// reflection VerifyCertificate_ValidCode_Returns200WithoutSensitiveFields (CertificateFlowTests).
    /// </summary>
    private static void AssertPublicDtoHasNoSensitiveFields()
    {
        string[] blockedKeywords = ["nisn", "kontak", "contact", "phone", "telp", "email"];
        var offending = typeof(PublicPortfolioDto).GetProperties()
            .Select(p => p.Name)
            .Where(name => blockedKeywords.Any(kw => name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (offending.Count > 0)
        {
            throw new InvalidOperationException($"PublicPortfolioDto mengandung field sensitif yang dilarang NFR-SEC-05: {string.Join(", ", offending)}");
        }
    }

    private static async Task<IResult> GetStudentPortfolioForStaff(Guid studentId, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null)
        {
            return Results.NotFound();
        }

        var portfolio = await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == student.Id, ct);
        var verifiedCompetencies = await GetVerifiedCompetenciesAsync(db, student.Id, ct);

        var sampleIds = ParseSampleIds(portfolio?.SampleJournalIdsCsv);
        var samples = sampleIds.Count == 0
            ? []
            : await db.JournalEntries.AsNoTracking()
                .Where(je => sampleIds.Contains(je.Id))
                .Select(je => new PortfolioJournalSampleDto(je.Id, je.Text, je.SubmittedAt))
                .ToListAsync(ct);

        var certificate = await (
            from p in db.Placements.AsNoTracking()
            where p.StudentId == student.Id
            join cert in db.Certificates.AsNoTracking() on p.Id equals cert.PlacementId
            select new PortfolioCertificateDto(cert.CertCode, cert.IssuedAt)
            ).FirstOrDefaultAsync(ct);

        return Results.Ok(new PortfolioDto(portfolio?.Headline, verifiedCompetencies, samples, certificate, portfolio?.IsPublished ?? false, portfolio?.Slug));
    }
}
