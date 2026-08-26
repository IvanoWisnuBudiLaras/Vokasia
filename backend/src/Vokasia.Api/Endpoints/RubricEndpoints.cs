using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
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
        rubrics.MapGet("/", ListRubrics).RequireAuthorization(RbacPolicies.TenantMember);

        var periods = app.MapGroup("/api/periods").WithTags("Rubrics");
        periods.MapGet("/{periodId:guid}/rubric", GetRubric).RequireAuthorization(RbacPolicies.TenantMember);

        return app;
    }

    private static bool WeightsSumTo100(IReadOnlyCollection<RubricAspectInput> aspects) =>
        RubricValidation.HasValidWeights(aspects.Select(a => a.Weight).ToArray());

    private static async Task<IResult> ListRubrics(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var templates = await db.RubricTemplates.AsNoTracking()
            .Include(t => t.Aspects)
            .Where(t => t.TenantId == tenant.TenantId && t.IsActive)
            .OrderByDescending(t => t.IsDefault)
            .ThenBy(t => t.CompanyId)
            .ThenByDescending(t => t.Version)
            .ToListAsync(ct);

        return Results.Ok(templates.Select(ToDto).ToList());
    }

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

        if (req.CompanyId.HasValue && !await db.TenantCompanies.AnyAsync(tc => tc.CompanyId == req.CompanyId.Value && tc.TenantId == tenant.TenantId, ct))
        {
            return Results.BadRequest(new { message = "DUDI tidak terhubung ke tenant ini." });
        }

        var matchingScope = db.RubricTemplates.Where(t => t.TenantId == tenant.TenantId && t.CompanyId == req.CompanyId);
        var version = (await matchingScope.Select(t => (int?)t.Version).MaxAsync(ct) ?? 0) + 1;
        var isDefault = !req.CompanyId.HasValue && !await db.RubricTemplates.AnyAsync(t => t.TenantId == tenant.TenantId && t.CompanyId == null && t.IsDefault && t.IsActive, ct);

        var template = new RubricTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId.Value,
            Name = req.Name,
            CompanyId = req.CompanyId,
            Version = version,
            IsDefault = isDefault,
            IsActive = true,
            Aspects = req.Aspects.Select(a => new RubricAspect { Id = Guid.NewGuid(), Name = a.Name, Description = a.Description, Kind = a.Kind, Weight = a.Weight }).ToList(),
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

        // Once a draft assessment references a template, its aspects are a snapshot. Keep the
        // historical template intact and publish a new version instead of mutating rows that an
        // in-progress assessment may already be using.
        var wasDefault = template.IsDefault;
        template.IsActive = false;
        template.IsDefault = false;
        var newTemplate = new RubricTemplate
        {
            Id = Guid.NewGuid(),
            TenantId = template.TenantId,
            Name = req.Name,
            CompanyId = template.CompanyId,
            Version = template.Version + 1,
            IsDefault = wasDefault,
            IsActive = true,
            Aspects = req.Aspects.Select(a => new RubricAspect
            {
                Id = Guid.NewGuid(),
                Name = a.Name,
                Description = a.Description,
                Kind = a.Kind,
                Weight = a.Weight,
            }).ToList(),
        };
        foreach (var aspect in newTemplate.Aspects)
        {
            aspect.RubricTemplateId = newTemplate.Id;
        }
        db.RubricTemplates.Add(newTemplate);

        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(newTemplate));
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
            .Where(t => t.TenantId == tenant.TenantId && t.CompanyId == null && t.IsDefault && t.IsActive)
            .OrderByDescending(t => t.Version)
            .FirstOrDefaultAsync(ct);
        if (template is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(ToDto(template));
    }

    internal static RubricDto ToDto(RubricTemplate t) => new(
        t.Id, t.Name, t.IsDefault,
        t.Aspects.Select(a => new RubricAspectDto(a.Id, a.Name, a.Kind, a.Weight, a.Description)).ToList(),
        t.CompanyId, t.Version, t.IsActive);
}
