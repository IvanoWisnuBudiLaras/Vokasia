using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Minio;
using Minio.DataModel.Args;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H5-E1 §1 — kunjungan monitoring guru ke DUDI (FR-ASM-01, wireframe W4). Policy `Teacher+`
/// SAJA (bukan resource-based per-placement seperti mentor) — sama pola dgn AddTeacherComment
/// (JournalEndpoints §4): DeptHead/TenantAdmin/Teacher tenant yg sama boleh mencatat kunjungan
/// utk placement mana pun di tenant-nya (query filter tenant EF sudah cukup scoping, guru
/// pembimbing spesifik BUKAN batasan keamanan di sini — beda dgn mentor DUDI yg lintas-tenant).
///
/// `TeacherId` pada Visit diisi dari `tenant.UserId` (caller sendiri) — BUKAN selalu sama dgn
/// `Placement.TeacherId` (guru lain di tenant yg sama bisa menggantikan kunjungan, mis. DeptHead
/// turun langsung) - dicatat siapa yg BENAR mencatat kunjungan itu, bukan siapa "pembimbing resmi".
///
/// Signature: FE kirim data URL base64 (`data:image/png;base64,...`) hasil canvas tanda tangan -
/// endpoint ini yang decode+upload PNG ke MinIO (BUKAN alur presign dua-tahap spt foto jurnal,
/// krn ukurannya kecil & one-shot, tak perlu upload langsung dari browser ke MinIO).
/// </summary>
public static class VisitEndpoints
{
    private const string BucketConfigKey = "Minio:Bucket";
    private const string DefaultBucket = "vokasia-journal";
    internal const long MaxSignatureBytes = 512 * 1024; // 512KB - tanda tangan PNG kecil, generous margin.

    public static IEndpointRouteBuilder MapVisitEndpoints(this IEndpointRouteBuilder app)
    {
        var visits = app.MapGroup("/api/placements/{placementId:guid}/visits").WithTags("Visits").AddEndpointFilter<ValidationFilter>();
        visits.MapPost("/", CreateVisit).RequireAuthorization(RbacPolicies.TeacherPlus);
        visits.MapGet("/", ListVisits).RequireAuthorization(RbacPolicies.TeacherPlus);
        // [GAP ditemukan+ditambal, VOK-H5-E2 (FE), lihat DECISIONS.md D34]: CreateVisit menerima
        // `PhotoKey` (objectKey MinIO yg SUDAH diunggah), tapi satu2nya endpoint presign yg ada
        // (JournalEndpoints.GetPresignedUploadUrl, "/api/journals/upload-url") dikunci policy
        // StudentSelf - Teacher/DeptHead/TenantAdmin (pencatat kunjungan §1) TAK PUNYA jalur presign
        // apa pun utk foto lokasi kunjungan, walau field-nya sudah ada di DTO sejak H5-E1. Endpoint
        // BARU minimal ini SENGAJA reuse persis UploadRequest/PresignedUploadDto (H3-E1) - bukan
        // DTO/validator baru - ValidationFilter global otomatis pakai UploadRequestValidator yg
        // sudah ada (whitelist ContentType+ukuran, sama persis jurnal). Prefix object key beda
        // ("visit-photo/", bukan "journal/") supaya tak campur namespace MinIO.
        visits.MapPost("/upload-url", GetVisitPhotoUploadUrl).RequireAuthorization(RbacPolicies.TeacherPlus);

        return app;
    }

    private static async Task<IResult> CreateVisit(
        Guid placementId, CreateVisitRequest req, VokasiaDbContext db, ITenantContext tenant,
        IMinioClient minio, IConfiguration config, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        if (!string.IsNullOrWhiteSpace(req.PhotoKey) &&
            !ObjectStorageKeyPolicy.IsOwnedKey(req.PhotoKey, tenant.TenantId.Value, "visit-photo"))
        {
            return Results.BadRequest(new { message = "PhotoKey harus berada di ruang penyimpanan tenant ini." });
        }

        var placementExists = await db.Placements.AnyAsync(p => p.Id == placementId, ct);
        if (!placementExists)
        {
            return Results.NotFound();
        }

        string? signatureKey;
        try
        {
            signatureKey = await TryUploadSignatureAsync(minio, config, tenant.TenantId.Value, req.SignatureDataUrl, ct);
        }
        catch (FormatException)
        {
            return Results.BadRequest(new { message = "SignatureDataUrl bukan base64 PNG yang valid." });
        }

        var visit = new Visit
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            PlacementId = placementId,
            TeacherId = tenant.UserId.Value,
            Date = req.Date,
            Notes = req.Notes,
            PhotoKey = req.PhotoKey,
            SignatureKey = signatureKey,
        };
        db.Visits.Add(visit);

        // AC dokumentasi ticket: "audit" — WriteAuditLog (AuditEndpoints) dirancang sbg endpoint HTTP
        // terpisah dipanggil actor (pola H2-E3, dipakai BFF utk TokenReuseDetected). Kunjungan ditulis
        // LANGSUNG ke tabel yang sama di sini (bukan lewat HTTP call balik ke diri sendiri) - actor
        // sudah pasti diri caller sendiri (tenant.UserId), tak perlu bulak-balik HTTP.
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ActorUserId = tenant.UserId.Value,
            Action = "VisitCreated",
            Entity = nameof(Visit),
            EntityId = visit.Id.ToString(),
            MetaJson = JsonSerializer.Serialize(new { visit.PlacementId, visit.Date }),
        });

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/placements/{placementId}/visits/{visit.Id}", ToDto(visit));
    }

    private static async Task<IResult> ListVisits(Guid placementId, VokasiaDbContext db, CancellationToken ct)
    {
        var placementExists = await db.Placements.AnyAsync(p => p.Id == placementId, ct);
        if (!placementExists)
        {
            return Results.NotFound();
        }

        var items = await db.Visits.AsNoTracking()
            .Where(v => v.PlacementId == placementId)
            .OrderByDescending(v => v.Date)
            .Select(v => ToDto(v))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static async Task<IResult> GetVisitPhotoUploadUrl(
        Guid placementId, UploadRequest req, IMinioClient minio, ITenantContext tenant, IConfiguration config, VokasiaDbContext db, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var placementExists = await db.Placements.AnyAsync(p => p.Id == placementId, ct);
        if (!placementExists)
        {
            return Results.NotFound();
        }

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var extension = req.ContentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "bin",
        };
        var objectKey = $"tenant/{tenant.TenantId}/visit-photo/{Guid.NewGuid():N}.{extension}";
        const int expirySeconds = 300;

        var url = await minio.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithExpiry(expirySeconds));

        return Results.Ok(new PresignedUploadDto(url, objectKey, expirySeconds));
    }

    private static async Task<string?> TryUploadSignatureAsync(
        IMinioClient minio, IConfiguration config, Guid tenantId, string? signatureDataUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(signatureDataUrl))
        {
            return null;
        }

        // "data:image/png;base64,AAAA..." -> ambil bagian setelah koma pertama. Kalau tak ada
        // prefix "data:" (klien kirim base64 mentah), anggap seluruh string adalah payload base64.
        var commaIndex = signatureDataUrl.IndexOf(',');
        var base64Payload = signatureDataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0
            ? signatureDataUrl[(commaIndex + 1)..]
            : signatureDataUrl;

        var bytes = Convert.FromBase64String(base64Payload); // FormatException kalau bukan base64 valid.
        if (bytes.Length > MaxSignatureBytes)
        {
            throw new FormatException($"Signature PNG melebihi batas {MaxSignatureBytes} bytes.");
        }

        var bucket = config[BucketConfigKey] ?? DefaultBucket;
        var objectKey = $"tenant/{tenantId}/visit-signature/{Guid.NewGuid():N}.png";

        using var stream = new MemoryStream(bytes);
        await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType("image/png"), ct);

        return objectKey;
    }

    private static VisitDto ToDto(Visit v) => new(v.Id, v.PlacementId, v.TeacherId, v.Date, v.Notes, v.PhotoKey, v.SignatureKey, v.CreatedAt);
}
