using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H5-E1 §2 — template rubrik penilaian (FR-ASM-02), policy `TenantAdmin` utk mutasi.
///
/// [CAKUPAN, dicatat eksplisit]: `GetRubric(periodId)` per SIGNATURE ticket ("rubrik aktif
/// PERIODE"), tapi skema `RubricTemplate` (H1-E1, dikonfirmasi TIDAK ada migrasi baru utk H5-E1)
/// TIDAK punya kolom PeriodId/relasi period→template - hanya `TenantId` + `IsDefault` per tenant.
/// MVP SENGAJA: satu rubric `IsDefault=true` dipakai LINTAS SEMUA periode tenant yang sama;
/// parameter `periodId` diterima (cocok signature AC) HANYA dipakai memvalidasi period itu ADA
/// dan milik tenant caller (404 kalau tidak), BUKAN utk memilih rubric berbeda per periode.
/// Kalau kelak produk butuh rubric berbeda per periode, itu perlu migrasi skema baru (keputusan
/// Developer, bukan silent scope-creep H5-E1) — dicatat juga di DECISIONS.md D33.
/// </summary>
public static class RubricEndpoints
{
    public static IEndpointRouteBuilder MapRubricEndpoints(this IEndpointRouteBuilder app)
    {
        var rubrics = app.MapGroup("/api/rubrics").WithTags("Rubrics").AddEndpointFilter<ValidationFilter>();
        rubrics.MapPost("/", CreateRubricTemplate).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        rubrics.MapPut("/{id:guid}", UpdateRubric).RequireAuthorization(RbacPolicies.TenantAdminOnly);

        var periods = app.MapGroup("/api/periods").WithTags("Rubrics");
        periods.MapGet("/{periodId:guid}/rubric", GetRubric).RequireAuthorization(RbacPolicies.TenantMember);

        return app;
    }

    private static bool WeightsSumTo100(IReadOnlyCollection<RubricAspectInput> aspects) =>
        aspects.Count > 0 && aspects.Sum(a => a.Weight) == 100;

    private static async Task<IResult> CreateRubricTemplate(CreateRubricRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        if (!WeightsSumTo100(req.Aspects))
        {
            return Results.UnprocessableEntity(new { message = "Total bobot (Weight) seluruh aspek rubrik harus tepat 100." });
        }

        var template = new RubricTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            Name = req.Name,
            IsDefault = !await db.RubricTemplates.AnyAsync(t => t.TenantId == tenant.TenantId, ct), // rubric pertama tenant otomatis jadi default.
            Aspects = req.Aspects.Select(a => new RubricAspect { Id = Guid.NewGuid(), Name = a.Name, Kind = a.Kind, Weight = a.Weight }).ToList(),
        };
        foreach (var aspect in template.Aspects)
        {
            aspect.RubricTemplateId = template.Id;
        }

        db.RubricTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/rubrics/{template.Id}", ToDto(template));
    }

    private static async Task<IResult> UpdateRubric(Guid id, UpdateRubricRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var template = await db.RubricTemplates.Include(t => t.Aspects).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
        {
            return Results.NotFound();
        }

        if (!WeightsSumTo100(req.Aspects))
        {
            return Results.UnprocessableEntity(new { message = "Total bobot (Weight) seluruh aspek rubrik harus tepat 100." });
        }

        // AC ticket: "ubah selama belum dipakai assessment final; sesudah → 409." Template BOLEH
        // sudah dipakai Assessment yang MASIH draft (IsFinal=false) - hanya FINAL yang mengunci.
        var usedByFinalAssessment = await db.Assessments.AnyAsync(a => a.RubricTemplateId == id && a.IsFinal, ct);
        if (usedByFinalAssessment)
        {
            return Results.Conflict(new { message = "Rubrik sudah dipakai assessment yang difinalisasi - tidak bisa diubah." });
        }

        template.Name = req.Name;
        db.RubricAspects.RemoveRange(template.Aspects);
        var newAspects = req.Aspects.Select(a => new RubricAspect { Id = Guid.NewGuid(), RubricTemplateId = template.Id, Name = a.Name, Kind = a.Kind, Weight = a.Weight }).ToList();
        db.RubricAspects.AddRange(newAspects);

        await db.SaveChangesAsync(ct);

        template.Aspects = newAspects;
        return Results.Ok(ToDto(template));
    }

    private static async Task<IResult> GetRubric(Guid periodId, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var periodExists = await db.Periods.AnyAsync(p => p.Id == periodId, ct);
        if (!periodExists)
        {
            return Results.NotFound();
        }

        // Lihat doc-comment kelas [CAKUPAN] - periodId hanya utk validasi keberadaan, rubric
        // yang dikembalikan adalah default TENANT (bukan spesifik per periode).
        var template = await db.RubricTemplates.Include(t => t.Aspects)
            .FirstOrDefaultAsync(t => t.TenantId == tenant.TenantId && t.IsDefault, ct);
        if (template is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToDto(template));
    }

    internal static RubricDto ToDto(RubricTemplate t) => new(
        t.Id, t.Name, t.IsDefault,
        t.Aspects.Select(a => new RubricAspectDto(a.Id, a.Name, a.Kind, a.Weight)).ToList());
}
