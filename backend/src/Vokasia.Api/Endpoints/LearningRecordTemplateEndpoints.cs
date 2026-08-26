using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Vokasia.Api.Authorization;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

public record LearningRecordTemplateCriterionInput(string Name, string Description);
public record CreateLearningRecordTemplateRequest(Guid CompanyId, List<LearningRecordTemplateCriterionInput> Criteria, Guid? PlacementId = null);
public record UpdateLearningRecordTemplateRequest(List<LearningRecordTemplateCriterionInput> Criteria);
public record LearningRecordTemplateCriterionDto(Guid Id, string Name, string Description, int SortOrder);
public record LearningRecordTemplateDto(Guid Id, Guid CompanyId, int Version, string Status, List<LearningRecordTemplateCriterionDto> Criteria);
public record PlacementLearningRecordSnapshotDto(
    Guid Id,
    Guid PlacementId,
    Guid CompanyId,
    Guid SourceTemplateId,
    int SourceTemplateVersion,
    string? CompanyDisplayName,
    string? PeriodDisplayName,
    DateOnly? PeriodStartDate,
    DateOnly? PeriodEndDate,
    List<LearningRecordTemplateCriterionDto> Criteria);

/// <summary>
/// Private V3 template lifecycle and placement snapshot boundary. It deliberately does not share
/// routes or mappings with V2 weighted rubrics.
/// </summary>
public static class LearningRecordTemplateEndpoints
{
    public static IEndpointRouteBuilder MapLearningRecordTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var templates = app.MapGroup("/api/learning-record/templates")
            .WithTags("Learning Record")
            .AddEndpointFilter<ValidationFilter>();
        templates.MapGet("/", ListTemplates).RequireAuthorization();
        templates.MapPost("/", CreateTemplate).RequireAuthorization();
        templates.MapPut("/{templateId:guid}", UpdateTemplate).RequireAuthorization();
        templates.MapPost("/{templateId:guid}/activate", ActivateTemplate).RequireAuthorization();

        var placements = app.MapGroup("/api/placements/{placementId:guid}/learning-record-snapshot")
            .WithTags("Learning Record");
        placements.MapGet("/", GetSnapshot).RequireAuthorization();
        placements.MapPost("/", CreateSnapshot).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> ListTemplates(
        Guid companyId,
        Guid? placementId,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        if (tenant.Role == nameof(UserRole.TenantAdmin) && tenant.TenantId.HasValue && tenant.UserId.HasValue)
        {
            if (!await db.TenantCompanies.AnyAsync(link => link.CompanyId == companyId, ct))
            {
                return Results.NotFound();
            }
        }
        else if (await FindAssignedMentorPlacementAsync(placementId, companyId, user, tenant, authorizationService, db, ct) is null)
        {
            return Results.Forbid();
        }

        var templates = await db.LearningRecordTemplates.AsNoTracking()
            .Include(template => template.Criteria)
            .Where(template => template.CompanyId == companyId)
            .OrderByDescending(template => template.Version)
            .ToListAsync(ct);
        return Results.Ok(templates.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreateTemplate(
        CreateLearningRecordTemplateRequest request,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        LearningRecordRules.ValidateTemplateCriterionCount(request.Criteria.Count);

        Guid tenantId;
        if (tenant.Role == nameof(UserRole.TenantAdmin) && tenant.TenantId.HasValue && tenant.UserId.HasValue)
        {
            tenantId = tenant.TenantId!.Value;
            if (!await db.TenantCompanies.AnyAsync(link => link.CompanyId == request.CompanyId, ct))
            {
                return Results.NotFound();
            }
        }
        else
        {
            var placement = await FindAssignedMentorPlacementAsync(request.PlacementId, request.CompanyId, user, tenant, authorizationService, db, ct);
            if (placement is null)
            {
                return Results.Forbid();
            }
            tenantId = placement.TenantId;
        }

        var version = (await db.LearningRecordTemplates
            .Where(template => template.TenantId == tenantId && template.CompanyId == request.CompanyId)
            .Select(template => (int?)template.Version)
            .MaxAsync(ct) ?? 0) + 1;
        var template = NewTemplate(tenantId, request.CompanyId, version, tenant.UserId!.Value, request.Criteria);
        db.LearningRecordTemplates.Add(template);
        WriteTemplateAudit(db, tenantId, tenant.UserId.Value, "LearningRecordTemplateCreated", template);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/learning-record/templates/{template.Id}", ToDto(template));
    }

    private static async Task<IResult> UpdateTemplate(
        Guid templateId,
        UpdateLearningRecordTemplateRequest request,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        LearningRecordRules.ValidateTemplateCriterionCount(request.Criteria.Count);
        var template = await db.LearningRecordTemplates.Include(candidate => candidate.Criteria)
            .FirstOrDefaultAsync(candidate => candidate.Id == templateId, ct);
        if (template is null)
        {
            return Results.NotFound();
        }
        var authResult = await authorizationService.AuthorizeAsync(user, template, new CanManageTemplateRequirement());
        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        if (template.Status == LearningRecordTemplateStatus.Superseded)
        {
            return Results.Conflict(new { message = "Versi template historis tidak dapat diubah." });
        }

        if (template.Status == LearningRecordTemplateStatus.Active)
        {
            var replacement = NewTemplate(template.TenantId, template.CompanyId, template.Version + 1, tenant.UserId!.Value, request.Criteria);
            db.LearningRecordTemplates.Add(replacement);
            WriteTemplateAudit(db, template.TenantId, tenant.UserId.Value, "LearningRecordTemplateVersionCreated", replacement);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/learning-record/templates/{replacement.Id}", ToDto(replacement));
        }

        db.LearningRecordTemplateCriteria.RemoveRange(template.Criteria);
        template.Criteria = BuildCriteria(template.Id, template.TenantId, request.Criteria);
        WriteTemplateAudit(db, template.TenantId, tenant.UserId!.Value, "LearningRecordTemplateDraftUpdated", template);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(template));
    }

    private static async Task<IResult> ActivateTemplate(
        Guid templateId,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        var template = await db.LearningRecordTemplates.Include(candidate => candidate.Criteria)
            .FirstOrDefaultAsync(candidate => candidate.Id == templateId, ct);
        if (template is null)
        {
            return Results.NotFound();
        }
        var authResult = await authorizationService.AuthorizeAsync(user, template, new CanManageTemplateRequirement());
        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        LearningRecordRules.ValidateTemplateCriterionCount(template.Criteria.Count);
        if (template.Status == LearningRecordTemplateStatus.Active)
        {
            return Results.Ok(ToDto(template));
        }

        var activeTemplates = await db.LearningRecordTemplates.Where(candidate =>
            candidate.TenantId == template.TenantId && candidate.CompanyId == template.CompanyId && candidate.Status == LearningRecordTemplateStatus.Active)
            .ToListAsync(ct);
        foreach (var activeTemplate in activeTemplates)
        {
            activeTemplate.Status = LearningRecordTemplateStatus.Superseded;
        }

        template.Status = LearningRecordTemplateStatus.Active;
        template.ActivatedAt = DateTimeOffset.UtcNow;
        WriteTemplateAudit(db, template.TenantId, tenant.UserId!.Value, "LearningRecordTemplateActivated", template);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(template));
    }

    private static async Task<IResult> GetSnapshot(
        Guid placementId,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        var placement = await db.Placements.FirstOrDefaultAsync(candidate => candidate.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }
        var authResult = await authorizationService.AuthorizeAsync(user, placement, new CanReadSnapshotRequirement());
        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        var snapshot = await db.PlacementLearningRecordSnapshots.AsNoTracking().Include(candidate => candidate.Criteria)
            .FirstOrDefaultAsync(candidate => candidate.PlacementId == placement.Id, ct);
        return snapshot is null ? Results.NotFound() : Results.Ok(ToDto(snapshot));
    }

    private static async Task<IResult> CreateSnapshot(
        Guid placementId,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        var placement = await db.Placements.FirstOrDefaultAsync(candidate => candidate.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }
        var authResult = await authorizationService.AuthorizeAsync(user, placement, new CanReadSnapshotRequirement());
        if (tenant.Role != nameof(UserRole.IndustryMentor) || !authResult.Succeeded)
        {
            return Results.Forbid();
        }

        var existing = await db.PlacementLearningRecordSnapshots.Include(candidate => candidate.Criteria)
            .FirstOrDefaultAsync(candidate => candidate.PlacementId == placement.Id, ct);
        if (existing is not null)
        {
            return Results.Ok(ToDto(existing));
        }

        await using IDbContextTransaction? transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        var snapshot = await CreateSnapshotForPlacementAsync(placement, tenant.UserId!.Value, db, ct);
        if (snapshot is null)
        {
            return Results.UnprocessableEntity(new { message = "DUDI belum memiliki template Learning Record aktif." });
        }

        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException exception) when (transaction is not null && IsPlacementSnapshotUniqueViolation(exception))
        {
            await transaction.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            var concurrentSnapshot = await db.PlacementLearningRecordSnapshots.AsNoTracking().Include(candidate => candidate.Criteria)
                .FirstAsync(candidate => candidate.PlacementId == placementId, ct);
            return Results.Ok(ToDto(concurrentSnapshot));
        }

        return Results.Created($"/api/placements/{placementId}/learning-record-snapshot", ToDto(snapshot));
    }

    /// <summary>
    /// Builds the immutable placement snapshot in the caller's transaction. Placement creation
    /// uses this same seam so every official placement is immediately valid for V3 assessment.
    /// </summary>
    internal static async Task<PlacementLearningRecordSnapshot?> CreateSnapshotForPlacementAsync(
        Placement placement,
        Guid actorUserId,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        var existing = await db.PlacementLearningRecordSnapshots.Include(candidate => candidate.Criteria)
            .FirstOrDefaultAsync(candidate => candidate.PlacementId == placement.Id, ct);
        if (existing is not null)
        {
            return existing;
        }

        var activeTemplate = await db.LearningRecordTemplates.Include(template => template.Criteria)
            .Where(template => template.TenantId == placement.TenantId && template.CompanyId == placement.CompanyId && template.Status == LearningRecordTemplateStatus.Active)
            .OrderByDescending(template => template.Version)
            .FirstOrDefaultAsync(ct);
        var activeCriteria = activeTemplate?.Criteria
            .Where(criterion => criterion.IsActive)
            .OrderBy(criterion => criterion.SortOrder)
            .ToList();
        if (activeTemplate is null || activeCriteria is null || activeCriteria.Count == 0)
        {
            return null;
        }

        var period = await db.Periods.AsNoTracking().SingleOrDefaultAsync(item => item.Id == placement.PeriodId, ct);
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == placement.CompanyId, ct);
        var snapshot = new PlacementLearningRecordSnapshot
        {
            Id = Guid.NewGuid(), TenantId = placement.TenantId, PlacementId = placement.Id, CompanyId = placement.CompanyId,
            SourceTemplateId = activeTemplate.Id, SourceTemplateVersion = activeTemplate.Version,
            CompanyDisplayName = company?.Name,
            PeriodDisplayName = period?.Name,
            PeriodStartDate = period?.StartDate,
            PeriodEndDate = period?.EndDate,
            Criteria = activeCriteria.Select(criterion => new PlacementLearningRecordCriterionSnapshot
            {
                Id = Guid.NewGuid(), TenantId = placement.TenantId, SnapshotId = Guid.Empty,
                Name = criterion.Name, Description = criterion.Description, SortOrder = criterion.SortOrder,
            }).ToList(),
        };
        foreach (var criterion in snapshot.Criteria)
        {
            criterion.SnapshotId = snapshot.Id;
        }

        db.PlacementLearningRecordSnapshots.Add(snapshot);
        WriteSnapshotAudit(db, placement, actorUserId, snapshot);
        return snapshot;
    }

    private static LearningRecordTemplate NewTemplate(
        Guid tenantId,
        Guid companyId,
        int version,
        Guid createdByUserId,
        IReadOnlyList<LearningRecordTemplateCriterionInput> criteria)
    {
        var template = new LearningRecordTemplate
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CompanyId = companyId, Version = version,
            CreatedByUserId = createdByUserId, Status = LearningRecordTemplateStatus.Draft,
        };
        template.Criteria = BuildCriteria(template.Id, tenantId, criteria);
        return template;
    }

    private static List<LearningRecordTemplateCriterion> BuildCriteria(
        Guid templateId,
        Guid tenantId,
        IReadOnlyList<LearningRecordTemplateCriterionInput> criteria) => criteria.Select((criterion, index) => new LearningRecordTemplateCriterion
    {
        Id = Guid.NewGuid(), TenantId = tenantId, TemplateId = templateId, Name = criterion.Name,
        Description = criterion.Description, SortOrder = index + 1, IsActive = true,
    }).ToList();

    private static void WriteTemplateAudit(
        VokasiaDbContext db,
        Guid tenantId,
        Guid actorUserId,
        string action,
        LearningRecordTemplate template) => db.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid(), TenantId = tenantId, ActorUserId = actorUserId, Action = action,
        Entity = nameof(LearningRecordTemplate), EntityId = template.Id.ToString(),
        MetaJson = JsonSerializer.Serialize(new { template.CompanyId, template.Version, Status = template.Status.ToString() }),
    });

    private static void WriteSnapshotAudit(
        VokasiaDbContext db,
        Placement placement,
        Guid actorUserId,
        PlacementLearningRecordSnapshot snapshot) => db.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid(), TenantId = placement.TenantId, ActorUserId = actorUserId,
        Action = "LearningRecordSnapshotCreated", Entity = nameof(Placement), EntityId = placement.Id.ToString(),
        MetaJson = JsonSerializer.Serialize(new
        {
            placementId = placement.Id,
            snapshotId = snapshot.Id,
            snapshot.SourceTemplateId,
            snapshot.SourceTemplateVersion,
        }),
    });

    private static bool IsPlacementSnapshotUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                       postgres.ConstraintName == "IX_PlacementLearningRecordSnapshots_PlacementId";
            }
        }

        return false;
    }

    private static LearningRecordTemplateDto ToDto(LearningRecordTemplate template) => new(
        template.Id, template.CompanyId, template.Version, template.Status.ToString(),
        template.Criteria.OrderBy(criterion => criterion.SortOrder).Select(ToDto).ToList());

    private static PlacementLearningRecordSnapshotDto ToDto(PlacementLearningRecordSnapshot snapshot) => new(
        snapshot.Id, snapshot.PlacementId, snapshot.CompanyId, snapshot.SourceTemplateId, snapshot.SourceTemplateVersion,
        snapshot.CompanyDisplayName, snapshot.PeriodDisplayName, snapshot.PeriodStartDate, snapshot.PeriodEndDate,
        snapshot.Criteria.OrderBy(criterion => criterion.SortOrder).Select(criterion => new LearningRecordTemplateCriterionDto(
            criterion.Id, criterion.Name, criterion.Description, criterion.SortOrder)).ToList());

    private static LearningRecordTemplateCriterionDto ToDto(LearningRecordTemplateCriterion criterion) => new(
        criterion.Id, criterion.Name, criterion.Description, criterion.SortOrder);

    private static async Task<Placement?> FindAssignedMentorPlacementAsync(
        Guid? placementId,
        Guid companyId,
        ClaimsPrincipal user,
        ITenantContext tenant,
        IAuthorizationService authorizationService,
        VokasiaDbContext db,
        CancellationToken ct)
    {
        if (tenant.Role != nameof(UserRole.IndustryMentor) || !placementId.HasValue)
        {
            return null;
        }

        var placement = await db.Placements.FirstOrDefaultAsync(
            candidate => candidate.Id == placementId.Value && candidate.CompanyId == companyId,
            ct);
        if (placement is null)
        {
            return null;
        }

        var authorization = await authorizationService.AuthorizeAsync(user, placement, RbacPolicies.MentorOwnPlacement);
        return authorization.Succeeded ? placement : null;
    }
}
