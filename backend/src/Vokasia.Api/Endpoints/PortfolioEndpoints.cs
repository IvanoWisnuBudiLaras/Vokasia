using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.Export;
using Vokasia.Api.RateLimiting;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.Configuration;

namespace Vokasia.Api.Endpoints;

/// <summary>Slice 6: structured student portfolio publication and safe public evidence delivery.</summary>
public static class PortfolioEndpoints
{
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";
    private const int MaxEvidence = 6;

    public static IEndpointRouteBuilder MapPortfolioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portfolio").WithTags("Portfolio")
            .RequireAuthorization(RbacPolicies.StudentSelf);

        group.MapGet("", GetMyPortfolio);
        group.MapPut("/", UpdatePortfolio).AddEndpointFilter<ValidationFilter>();
        group.MapPost("/publish", PublishPortfolio);
        group.MapPost("/unpublish", UnpublishPortfolio);

        app.MapGet("/api/portfolio/student/{studentId:guid}", GetStudentPortfolioForStaff)
            .RequireAuthorization(RbacPolicies.TenantMember);

        app.MapGet("/p/{slug}", GetPublicPortfolio)
            .RequireRateLimiting(VokasiaRateLimiting.PublicPolicy);
        app.MapGet("/api/public/portfolio/{slug}/cv", GetPublicCv)
            .RequireRateLimiting(VokasiaRateLimiting.PublicPolicy);
        app.MapGet("/api/public/portfolio/{slug}/evidence/{index:int}", GetPublicEvidence)
            .RequireRateLimiting(VokasiaRateLimiting.PublicPolicy);

        return app;
    }

    private sealed record EvidenceRow(Guid JournalEntryId, string Text, DateTimeOffset SubmittedAt, string? MediaKey);
    private sealed record PlacementPublicInfo(string CompanyName, string PeriodLabel, DateOnly StartDate, DateOnly EndDate);

    private static Task<Student?> FindCallerStudentAsync(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct) =>
        db.Students.FirstOrDefaultAsync(s => s.UserId == tenant.UserId, ct);

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

    private static IQueryable<JournalEntry> ApprovedEntriesForStudent(VokasiaDbContext db, Guid studentId) =>
        from je in db.JournalEntries.AsNoTracking()
        join p in db.Placements.AsNoTracking() on je.PlacementId equals p.Id
        where p.StudentId == studentId && je.Status == JournalEntryStatus.Approved
        select je;

    private static async Task<List<Guid>> GetLatestApprovedIdsAsync(VokasiaDbContext db, Guid studentId, CancellationToken ct) =>
        await ApprovedEntriesForStudent(db, studentId)
            .OrderByDescending(je => je.SubmittedAt)
            .ThenByDescending(je => je.Id)
            .Take(MaxEvidence)
            .Select(je => je.Id)
            .ToListAsync(ct);

    private static async Task<List<EvidenceRow>> GetEvidenceRowsAsync(
        VokasiaDbContext db,
        Guid studentId,
        IReadOnlyList<Guid> requestedIds,
        CancellationToken ct)
    {
        var ids = requestedIds.Count > 0
            ? requestedIds.Distinct().Take(MaxEvidence).ToList()
            : await GetLatestApprovedIdsAsync(db, studentId, ct);

        if (ids.Count == 0)
        {
            return [];
        }

        var journals = await ApprovedEntriesForStudent(db, studentId)
            .Where(je => ids.Contains(je.Id))
            .Select(je => new { je.Id, je.Text, je.SubmittedAt })
            .ToListAsync(ct);
        var byId = journals.ToDictionary(j => j.Id);
        var photoKeys = await db.JournalPhotos.AsNoTracking()
            .Where(photo => ids.Contains(photo.JournalEntryId) && photo.Status == PhotoStatus.Processed)
            .Select(photo => new { photo.JournalEntryId, photo.ObjectKey, photo.ThumbKey })
            .ToListAsync(ct);

        var ordered = new List<EvidenceRow>();
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var journal))
            {
                continue;
            }

            var photo = photoKeys
                .Where(candidate => candidate.JournalEntryId == id)
                .OrderBy(candidate => candidate.ObjectKey)
                .FirstOrDefault();
            ordered.Add(new EvidenceRow(journal.Id, RichTextDocument.ToPlainText(journal.Text), journal.SubmittedAt, photo?.ThumbKey ?? photo?.ObjectKey));
        }

        return ordered
            .OrderByDescending(row => row.SubmittedAt)
            .ThenByDescending(row => row.JournalEntryId)
            .ToList();
    }

    private static async Task<PlacementPublicInfo?> GetLatestPlacementAsync(VokasiaDbContext db, Guid studentId, CancellationToken ct) =>
        await (
            from placement in db.Placements.AsNoTracking()
            where placement.StudentId == studentId
            join company in db.Companies.AsNoTracking() on placement.CompanyId equals company.Id
            join period in db.Periods.AsNoTracking() on placement.PeriodId equals period.Id
            orderby period.EndDate descending
            select new PlacementPublicInfo(company.Name, period.Name, period.StartDate, period.EndDate)
        ).FirstOrDefaultAsync(ct);

    private static async Task<List<string>> GetMissingRequirementsAsync(
        VokasiaDbContext db,
        Student student,
        string? headline,
        CancellationToken ct)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(headline))
        {
            missing.Add("Tambahkan deskripsi singkat pengalaman PKL.");
        }

        if (await GetLatestPlacementAsync(db, student.Id, ct) is null)
        {
            missing.Add("Lengkapi data penempatan PKL.");
        }

        return missing;
    }

    private static async Task<PortfolioCertificateDto?> GetPortfolioCertificateAsync(VokasiaDbContext db, Guid studentId, CancellationToken ct) =>
        await (
            from placement in db.Placements.AsNoTracking()
            where placement.StudentId == studentId
            join cert in db.Certificates.AsNoTracking() on placement.Id equals cert.PlacementId
            orderby cert.IssuedAt descending
            select new PortfolioCertificateDto(
                cert.CertCode,
                cert.IssuedAt,
                cert.RevokedAt.HasValue ? CertificateVerificationStatus.Revoked : CertificateVerificationStatus.Valid,
                cert.RevokedAt,
                cert.PublicRevocationReason)
        ).FirstOrDefaultAsync(ct);

    private static List<Guid> ParseIds(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

    private static bool SequenceEqualIds(IReadOnlyList<Guid> left, IReadOnlyList<Guid> right) =>
        left.Count == right.Count && left.SequenceEqual(right);

    private static async Task<PortfolioDto> BuildPrivatePortfolioAsync(VokasiaDbContext db, Student student, Portfolio? portfolio, CancellationToken ct)
    {
        var verifiedCompetencies = await GetVerifiedCompetenciesAsync(db, student.Id, ct);
        var draftIds = ParseIds(portfolio?.DraftSampleJournalIdsCsv);
        if (draftIds.Count == 0)
        {
            draftIds = portfolio?.IsPublished == true ? ParseIds(portfolio.SampleJournalIdsCsv) : await GetLatestApprovedIdsAsync(db, student.Id, ct);
        }

        var evidence = await GetEvidenceRowsAsync(db, student.Id, draftIds, ct);
        var samples = evidence.Select(row => new PortfolioJournalSampleDto(row.JournalEntryId, row.Text, row.SubmittedAt)).ToList();
        var draftHeadline = portfolio?.DraftHeadline ?? portfolio?.Headline;
        var draftSavedIds = ParseIds(portfolio?.DraftSampleJournalIdsCsv);
        var publishedIds = ParseIds(portfolio?.SampleJournalIdsCsv);
        var hasUnpublishedChanges = portfolio?.IsPublished == true &&
            ((portfolio.DraftHeadline ?? portfolio.Headline) != portfolio.Headline ||
             (draftSavedIds.Count > 0 && !SequenceEqualIds(draftSavedIds, publishedIds)));
        var missing = await GetMissingRequirementsAsync(db, student, draftHeadline, ct);
        var certificate = await GetPortfolioCertificateAsync(db, student.Id, ct);

        return new PortfolioDto(draftHeadline, verifiedCompetencies, samples, certificate, portfolio?.IsPublished ?? false, portfolio?.Slug, hasUnpublishedChanges, missing);
    }

    private static async Task<IResult> GetMyPortfolio(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var student = await FindCallerStudentAsync(db, tenant, ct);
        return student is null
            ? Results.Forbid()
            : Results.Ok(await BuildPrivatePortfolioAsync(db, student, await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == student.Id, ct), ct));
    }

    private static async Task<IResult> UpdatePortfolio(UpdatePortfolioRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
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

        // An empty headline is an explicit draft value. Keeping it distinct from null prevents
        // PublishPortfolio from falling back to the previous published headline after a clear.
        portfolio.DraftHeadline = req.Headline?.Trim() ?? string.Empty;
        portfolio.DraftSampleJournalIdsCsv = string.Join(",", await GetLatestApprovedIdsAsync(db, student.Id, ct));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> PublishPortfolio(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        AssertPublicDtoHasNoSensitiveFields();
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

        portfolio.DraftHeadline ??= portfolio.Headline;
        if (string.IsNullOrWhiteSpace(portfolio.DraftSampleJournalIdsCsv))
        {
            portfolio.DraftSampleJournalIdsCsv = string.Join(",", await GetLatestApprovedIdsAsync(db, student.Id, ct));
        }

        var missing = await GetMissingRequirementsAsync(db, student, portfolio.DraftHeadline, ct);
        if (missing.Count > 0)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Publication"] = missing.ToArray() });
        }

        if (string.IsNullOrEmpty(portfolio.Slug))
        {
            var major = await db.Majors.AsNoTracking().FirstOrDefaultAsync(m => m.Id == student.MajorId, ct);
            var placement = await GetLatestPlacementAsync(db, student.Id, ct);
            portfolio.Slug = await GenerateUniqueSlugAsync(db, student.FullName, major?.Name ?? "umum", placement?.EndDate.Year ?? DateTime.UtcNow.Year, ct);
        }

        portfolio.Headline = portfolio.DraftHeadline;
        portfolio.SampleJournalIdsCsv = portfolio.DraftSampleJournalIdsCsv;
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

        portfolio.IsPublished = false;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetPublicPortfolio(string slug, VokasiaDbContext db, HttpContext http, CancellationToken ct)
    {
        var portfolio = await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        if (portfolio is null)
        {
            return Results.NotFound();
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == portfolio.StudentId, ct);
        var tenant = student is null ? null : await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == student.TenantId, ct);
        var major = student is null ? null : await db.Majors.AsNoTracking().FirstOrDefaultAsync(m => m.Id == student.MajorId, ct);
        var placement = student is null ? null : await GetLatestPlacementAsync(db, student.Id, ct);
        if (student is null || tenant is null || placement is null)
        {
            return Results.NotFound();
        }

        var verifiedCompetencies = await GetVerifiedCompetenciesAsync(db, student.Id, ct);
        var evidenceRows = await GetEvidenceRowsAsync(db, student.Id, ParseIds(portfolio.SampleJournalIdsCsv), ct);
        var evidence = evidenceRows.Select((row, index) => new PublicPortfolioEvidenceDto(row.Text, row.SubmittedAt, row.MediaKey is null ? null : $"/p/{Uri.EscapeDataString(slug)}/evidence/{index}")).ToList();
        var certificate = await (
            from placementRow in db.Placements.AsNoTracking()
            where placementRow.StudentId == student.Id
            join cert in db.Certificates.AsNoTracking() on placementRow.Id equals cert.PlacementId
            orderby cert.IssuedAt descending
            select new PublicPortfolioCertificateDto(cert.CertCode, cert.IssuedAt, cert.RevokedAt.HasValue ? CertificateVerificationStatus.Revoked : CertificateVerificationStatus.Valid, cert.RevokedAt, cert.PublicRevocationReason)
        ).FirstOrDefaultAsync(ct);

        http.Response.Headers.CacheControl = "public, max-age=300";
        var durationLabel = $"{Math.Max(1, (placement.EndDate.ToDateTime(TimeOnly.MinValue) - placement.StartDate.ToDateTime(TimeOnly.MinValue)).Days / 30)} bulan";
        return Results.Ok(new PublicPortfolioDto(student.FullName, tenant.SchoolName, major?.Name ?? "-", placement.PeriodLabel, placement.CompanyName, durationLabel, portfolio.Headline, verifiedCompetencies, evidence, certificate));
    }

    private static async Task<IResult> GetPublicCv(string slug, VokasiaDbContext db, IConfiguration config, HttpContext http, CancellationToken ct)
    {
        var portfolio = await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        if (portfolio is null)
        {
            return Results.NotFound();
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == portfolio.StudentId, ct);
        var tenant = student is null ? null : await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == student.TenantId, ct);
        var major = student is null ? null : await db.Majors.AsNoTracking().FirstOrDefaultAsync(m => m.Id == student.MajorId, ct);
        var placement = student is null ? null : await GetLatestPlacementAsync(db, student.Id, ct);
        if (student is null || tenant is null || major is null || placement is null)
        {
            return Results.NotFound();
        }

        var certificate = await (
            from placementRow in db.Placements.AsNoTracking()
            where placementRow.StudentId == student.Id
            join cert in db.Certificates.AsNoTracking() on placementRow.Id equals cert.PlacementId
            orderby cert.IssuedAt descending
            select new { cert.CertCode, cert.IssuedAt }
        ).FirstOrDefaultAsync(ct);

        var publicUrl = PublicAppOrigin.Resolve(config);
        var portfolioUrl = $"{publicUrl.TrimEnd('/')}/p/{Uri.EscapeDataString(slug)}";
        var verificationUrl = certificate is null ? null : $"{publicUrl.TrimEnd('/')}/verify/{certificate.CertCode}";
        var competencies = await GetVerifiedCompetenciesAsync(db, student.Id, ct);
        var durationLabel = $"{Math.Max(1, (placement.EndDate.ToDateTime(TimeOnly.MinValue) - placement.StartDate.ToDateTime(TimeOnly.MinValue)).Days / 30)} bulan";
        var pdf = AtsCvPdfWriter.Write(new AtsCvData(
            student.FullName,
            "Kontak: melalui portofolio publik Vokasia",
            tenant.SchoolName,
            major.Name,
            placement.CompanyName,
            placement.PeriodLabel,
            durationLabel,
            portfolio.Headline,
            competencies,
            certificate?.CertCode,
            certificate?.IssuedAt,
            verificationUrl,
            portfolioUrl));

        http.Response.Headers.CacheControl = "private, no-store";
        return Results.File(pdf, "application/pdf", $"cv-{Slugify(student.FullName)}.pdf");
    }

    private static async Task<IResult> GetPublicEvidence(string slug, int index, VokasiaDbContext db, IMinioClient minio, IConfiguration config, HttpContext http, CancellationToken ct)
    {
        if (index < 0 || index >= MaxEvidence)
        {
            return Results.NotFound();
        }

        var portfolio = await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
        if (portfolio is null)
        {
            return Results.NotFound();
        }

        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == portfolio.StudentId, ct);
        if (student is null)
        {
            return Results.NotFound();
        }

        var rows = await GetEvidenceRowsAsync(db, student.Id, ParseIds(portfolio.SampleJournalIdsCsv), ct);
        if (index >= rows.Count || rows[index].MediaKey is null || !ObjectStorageKeyPolicy.IsOwnedKey(rows[index].MediaKey, student.TenantId, "journal"))
        {
            return Results.NotFound();
        }

        try
        {
            await using var image = new MemoryStream();
            await minio.GetObjectAsync(new GetObjectArgs().WithBucket(config[BucketConfigKey] ?? DefaultBucket).WithObject(rows[index].MediaKey).WithCallbackStream(stream => stream.CopyTo(image)), ct);
            http.Response.Headers.CacheControl = "public, max-age=300";
            return Results.File(image.ToArray(), "image/jpeg");
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return Results.NotFound();
        }
    }

    private static async Task<string> GenerateUniqueSlugAsync(VokasiaDbContext db, string fullName, string majorName, int year, CancellationToken ct)
    {
        var baseSlug = Slugify($"{fullName}-{majorName}-{year}");
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
        var chars = input.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var collapsed = new string(chars);
        while (collapsed.Contains("--"))
        {
            collapsed = collapsed.Replace("--", "-");
        }

        return collapsed.Trim('-');
    }

    private static void AssertPublicDtoHasNoSensitiveFields()
    {
        string[] blockedKeywords = ["nisn", "kontak", "contact", "phone", "telp", "email", "tenantid", "objectkey", "bucket"];
        var offending = typeof(PublicPortfolioDto).GetProperties()
            .Select(property => property.Name)
            .Where(name => blockedKeywords.Any(keyword => name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (offending.Count > 0)
        {
            throw new InvalidOperationException($"PublicPortfolioDto mengandung field publik yang dilarang: {string.Join(", ", offending)}");
        }
    }

    private static async Task<IResult> GetStudentPortfolioForStaff(Guid studentId, VokasiaDbContext db, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId, ct);
        if (student is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(await BuildPrivatePortfolioAsync(db, student, await db.Portfolios.AsNoTracking().FirstOrDefaultAsync(p => p.StudentId == student.Id, ct), ct));
    }
}
