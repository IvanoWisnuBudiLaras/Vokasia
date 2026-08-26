using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.RateLimiting;
using Vokasia.Api.Storage;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>Certificate download, public verification, and tenant-admin revocation.</summary>
public static class CertificateEndpoints
{
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";

    public static IEndpointRouteBuilder MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/placements/{placementId:guid}/certificate", GetCertificate).RequireAuthorization();

        var revoke = app.MapGroup("/api/placements/{placementId:guid}/certificate")
            .WithTags("Certificate")
            .RequireAuthorization(RbacPolicies.TenantAdminOnly)
            .AddEndpointFilter<ValidationFilter>();
        revoke.MapPost("/revoke", RevokeCertificate);

        app.MapGet("/api/verify/{certCode}", VerifyCertificate)
            .RequireRateLimiting(VokasiaRateLimiting.PublicPolicy);
        app.MapGet("/api/verify/{certCode}/pdf", GetPublicCertificatePdf)
            .RequireRateLimiting(VokasiaRateLimiting.PublicPolicy);

        return app;
    }

    private static async Task<IResult> GetCertificate(
        Guid placementId,
        ITenantContext tenant,
        VokasiaDbContext db,
        IBrowserObjectStorageSigner storageSigner,
        IConfiguration config,
        CancellationToken ct)
    {
        if (!tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var placement = await db.Placements.AsNoTracking().FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        var isAdmin = tenant.Role is nameof(UserRole.TenantAdmin) or nameof(UserRole.DeptHead) or nameof(UserRole.SuperAdmin);
        if (!isAdmin && !await db.Students.AsNoTracking().AnyAsync(s => s.Id == placement.StudentId && s.UserId == tenant.UserId, ct))
        {
            return Results.Forbid();
        }

        var certificate = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.PlacementId == placementId, ct);
        if (certificate is null || !ObjectStorageKeyPolicy.IsOwnedKey(certificate.PdfKey, certificate.TenantId, "certificates"))
        {
            return Results.NotFound();
        }

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var url = await storageSigner.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(certificate.PdfKey)
            .WithExpiry(24 * 60 * 60));
        return Results.Ok(new CertificateDownloadDto(url));
    }

    private static async Task<IResult> RevokeCertificate(
        Guid placementId,
        RevokeCertificateRequest req,
        ITenantContext tenant,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        var placement = await db.Placements.FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        var certificate = await db.Certificates.FirstOrDefaultAsync(c => c.PlacementId == placementId, ct);
        if (certificate is null)
        {
            return Results.NotFound();
        }

        if (certificate.RevokedAt.HasValue)
        {
            return Results.Conflict(new { message = "Sertifikat sudah dicabut." });
        }

        certificate.RevokedAt = DateTimeOffset.UtcNow;
        certificate.PublicRevocationReason = req.PublicReason.Trim();
        certificate.InternalRevocationNote = string.IsNullOrWhiteSpace(req.InternalNote) ? null : req.InternalNote.Trim();
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = placement.TenantId,
            ActorUserId = tenant.UserId!.Value,
            Action = "CertificateRevoked",
            Entity = nameof(Certificate),
            EntityId = certificate.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { certificate.CertCode, certificate.PublicRevocationReason }),
        });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> VerifyCertificate(string certCode, VokasiaDbContext db, CancellationToken ct)
    {
        var result = await (
            from cert in db.Certificates.AsNoTracking()
            where cert.CertCode == certCode
            join placement in db.Placements.AsNoTracking() on cert.PlacementId equals placement.Id
            join student in db.Students.AsNoTracking() on placement.StudentId equals student.Id
            join major in db.Majors.AsNoTracking() on student.MajorId equals major.Id into majorRows
            from major in majorRows.DefaultIfEmpty()
            join company in db.Companies.AsNoTracking() on placement.CompanyId equals company.Id
            join period in db.Periods.AsNoTracking() on placement.PeriodId equals period.Id
            join tenant in db.Tenants.AsNoTracking() on placement.TenantId equals tenant.Id
            select new
            {
                cert.CertCode,
                StudentName = student.FullName,
                SchoolName = tenant.SchoolName,
                MajorName = major == null ? "-" : major.Name,
                CompanyName = company.Name,
                PeriodLabel = period.Name,
                cert.IssuedAt,
                cert.RevokedAt,
                cert.PublicRevocationReason,
            }
        ).FirstOrDefaultAsync(ct);

        if (result is null)
        {
            return Results.NotFound();
        }

        var status = result.RevokedAt.HasValue ? CertificateVerificationStatus.Revoked : CertificateVerificationStatus.Valid;
        return Results.Ok(new VerifyCertificateDto(
            result.CertCode,
            result.StudentName,
            result.SchoolName,
            result.MajorName,
            result.CompanyName,
            result.PeriodLabel,
            result.IssuedAt,
            status,
            result.RevokedAt,
            result.PublicRevocationReason,
            status == CertificateVerificationStatus.Valid));
    }

    private static async Task<IResult> GetPublicCertificatePdf(
        string certCode,
        VokasiaDbContext db,
        IMinioClient minio,
        IConfiguration config,
        CancellationToken ct)
    {
        var certificate = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.CertCode == certCode, ct);
        if (certificate is null || !ObjectStorageKeyPolicy.IsOwnedKey(certificate.PdfKey, certificate.TenantId, "certificates"))
        {
            return Results.NotFound();
        }

        try
        {
            await using var pdf = new MemoryStream();
            await minio.GetObjectAsync(new GetObjectArgs()
                .WithBucket(config[BucketConfigKey] ?? DefaultBucket)
                .WithObject(certificate.PdfKey)
                .WithCallbackStream(stream => stream.CopyTo(pdf)), ct);
            return Results.File(pdf.ToArray(), "application/pdf");
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return Results.NotFound();
        }
    }
}
