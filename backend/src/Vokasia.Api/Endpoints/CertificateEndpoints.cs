using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.RateLimiting;
using Vokasia.Api.Storage;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H5-E1 §5 — GetCertificate (unduh presigned, siswa sendiri/admin) + VerifyCertificate
/// (publik, rate-limit "public", TANPA data sensitif - FR-CRT-02: tanpa NISN/kontak/nilai).
/// </summary>
public static class CertificateEndpoints
{
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";
    private const int PresignedExpirySeconds = 24 * 60 * 60;

    public static IEndpointRouteBuilder MapCertificateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/placements/{placementId:guid}/certificate", GetCertificate).RequireAuthorization();

        // Anonim BY DESIGN (verifikasi publik oleh siapa saja yg pegang kertas sertifikat/CertCode)
        // - rate limit "public" (pola sama MagicLinkEndpoints.Validate, brute-force CertCode acak).
        app.MapGet("/api/verify/{certCode}", VerifyCertificate).RequireRateLimiting(VokasiaRateLimiting.PublicPolicy);

        return app;
    }

    private static async Task<IResult> GetCertificate(Guid placementId, ITenantContext tenant, VokasiaDbContext db, IBrowserObjectStorageSigner storageSigner, IConfiguration config, CancellationToken ct)
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
        if (!isAdmin)
        {
            var isOwnStudent = await db.Students.AsNoTracking().AnyAsync(s => s.Id == placement.StudentId && s.UserId == tenant.UserId, ct);
            if (!isOwnStudent)
            {
                return Results.Forbid();
            }
        }

        var certificate = await db.Certificates.AsNoTracking().FirstOrDefaultAsync(c => c.PlacementId == placementId, ct);
        if (certificate is null)
        {
            return Results.NotFound();
        }

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var url = await storageSigner.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(certificate.PdfKey)
            .WithExpiry(PresignedExpirySeconds));

        return Results.Ok(new CertificateDownloadDto(url));
    }

    private static async Task<IResult> VerifyCertificate(string certCode, VokasiaDbContext db, CancellationToken ct)
    {
        // TenantId ambient null di sini (endpoint publik, tak ada JWT) -> filter tenant EF otomatis
        // "mati" (pola sama JournalEndpoints mentor lintas-tenant) - pencarian CertCode SENGAJA
        // lintas SEMUA tenant (siapa pun bisa verifikasi sertifikat sekolah mana pun, itu tujuannya).
        var result = await (
            from cert in db.Certificates.AsNoTracking()
            where cert.CertCode == certCode
            join p in db.Placements.AsNoTracking() on cert.PlacementId equals p.Id
            join s in db.Students.AsNoTracking() on p.StudentId equals s.Id
            join c in db.Companies.AsNoTracking() on p.CompanyId equals c.Id
            join per in db.Periods.AsNoTracking() on p.PeriodId equals per.Id
            join t in db.Tenants.AsNoTracking() on p.TenantId equals t.Id
            select new VerifyCertificateDto(s.FullName, t.SchoolName, c.Name, per.Name, cert.IssuedAt, true)
            ).FirstOrDefaultAsync(ct);

        // AC: "certCode valid/palsu, Then verify 200 minimal-data / 404" - 404 (bukan 200
        // {Valid=false}) supaya tak beri sinyal berbeda antara "salah ketik" vs "kode ada tapi
        // tak valid" (kode yg tersimpan MEMANG selalu valid sejak diterbitkan - tak ada status
        // revoked di skema H5-E1, jadi "ada di DB" = "valid", "tak ada" = 404 murni).
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
