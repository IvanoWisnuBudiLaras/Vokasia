using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H5-E1 §3 — skor dua sisi (mentor: Teknis+Kehadiran industri; guru: Softskill+lainnya
/// sekolah - lihat doc-comment AssessmentScoring), finalisasi terkunci, satu DTO baca (GetAssessment).
///
/// Assessment DIBUAT LAZY (find-or-create) saat submit skor PERTAMA kali utk suatu placement -
/// TIDAK ada endpoint "CreateAssessment" terpisah di ticket. RubricTemplateId diambil dari rubric
/// `IsDefault=true` tenant SAAT ITU (snapshot - assessment yg sudah dibuat TIDAK ikut berubah kalau
/// admin ganti rubric default belakangan, by design: RubricEndpoints.UpdateRubric mengunci 409 kalau
/// rubric sudah dipakai assessment FINAL, tapi assessment draft/belum-final tetap bisa "kehilangan
/// sinkron" dgn rubric baru - gap yang SENGAJA didokumentasikan, bukan ditutup diam-diam, krn di
/// luar cakupan literal AC ticket ini).
/// </summary>
public static class AssessmentEndpoints
{
    public static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/placements/{placementId:guid}/assessment").WithTags("Assessment").AddEndpointFilter<ValidationFilter>();

        // Mentor: resource-based (sama pola ApproveJournal - lihat komentar JournalEndpoints §3).
        group.MapPost("/mentor-scores", SubmitMentorScores).RequireAuthorization();
        group.MapPost("/teacher-scores", SubmitTeacherScores).RequireAuthorization(RbacPolicies.TeacherPlus);
        group.MapGet("/", GetAssessment).RequireAuthorization();

        var periods = app.MapGroup("/api/periods/{periodId:guid}/assessments").WithTags("Assessment").AddEndpointFilter<ValidationFilter>();
        periods.MapPost("/finalize", FinalizeAssessment).RequireAuthorization(RbacPolicies.TenantAdminOnly);

        // [GAP ditambal, VOK-H5-E2 (FE), lihat DECISIONS.md D34] — lihat doc-comment MentorAssessmentPlacementDto (Dtos.cs).
        app.MapGet("/api/mentors/assessment-queue", ListMentorAssessmentPlacements).WithTags("Assessment").RequireAuthorization();

        return app;
    }

    private static async Task<IResult> ListMentorAssessmentPlacements(ITenantContext tenant, VokasiaDbContext db, CancellationToken ct)
    {
        if (tenant.Role != nameof(UserRole.IndustryMentor) || !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        // TenantId ambient NULL utk mentor (lintas-tenant by design, sama pola JournalEndpoints
        // GetPendingApprovals) - filter tenant EF otomatis "mati", query LINTAS SEMUA tenant sengaja.
        var rows = await (
            from p in db.Placements.AsNoTracking()
            where p.MentorUserId == tenant.UserId.Value
            join per in db.Periods.AsNoTracking() on p.PeriodId equals per.Id
            where per.Status == PeriodStatus.Assessment
            join s in db.Students.AsNoTracking() on p.StudentId equals s.Id
            join c in db.Companies.AsNoTracking() on p.CompanyId equals c.Id
            select new MentorAssessmentPlacementDto(p.Id, s.FullName, c.Name, per.Name)
            ).ToListAsync(ct);

        return Results.Ok(rows);
    }

    /// <summary>Teknis+Kehadiran = sisi DUDI/mentor; sisanya (Softskill) = sisi sekolah/guru (AC §3).</summary>
    internal static bool IsMentorSide(RubricAspectKind kind) => kind is RubricAspectKind.Teknis or RubricAspectKind.Kehadiran;

    private static async Task<RubricTemplate?> ResolveRubricAsync(VokasiaDbContext db, Assessment? existing, Guid tenantId, CancellationToken ct)
    {
        if (existing is not null)
        {
            return await db.RubricTemplates.Include(t => t.Aspects).FirstOrDefaultAsync(t => t.Id == existing.RubricTemplateId, ct);
        }
        return await db.RubricTemplates.Include(t => t.Aspects).FirstOrDefaultAsync(t => t.TenantId == tenantId && t.IsDefault, ct);
    }

    private static async Task<IResult> SubmitMentorScores(
        Guid placementId, List<ScoreInput> req, System.Security.Claims.ClaimsPrincipal user,
        IAuthorizationService authService, VokasiaDbContext db, CancellationToken ct)
    {
        var placement = await db.Placements.FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        var authResult = await authService.AuthorizeAsync(user, placement, RbacPolicies.MentorOwnPlacement);
        if (!authResult.Succeeded)
        {
            return Results.Forbid();
        }

        var sub = user.FindFirst(Claims.Subject)?.Value;
        if (!Guid.TryParse(sub, out var mentorUserId))
        {
            return Results.Forbid();
        }

        return await SubmitScoresAsync(db, placement, req, ScoredBy.Mentor, mentorUserId, aspect => IsMentorSide(aspect.Kind), ct);
    }

    private static async Task<IResult> SubmitTeacherScores(
        Guid placementId, List<ScoreInput> req, ITenantContext tenant, VokasiaDbContext db, CancellationToken ct)
    {
        if (!tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var placement = await db.Placements.FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        // TeacherPlus authenticates the role; it does not establish row ownership.
        if (tenant.Role == nameof(UserRole.Teacher) && placement.TeacherId != tenant.UserId.Value)
        {
            return Results.Forbid();
        }

        return await SubmitScoresAsync(db, placement, req, ScoredBy.Teacher, tenant.UserId.Value, aspect => !IsMentorSide(aspect.Kind), ct);
    }

    private static async Task<IResult> SubmitScoresAsync(
        VokasiaDbContext db, Placement placement, List<ScoreInput> req, ScoredBy side, Guid scorerUserId,
        Func<RubricAspect, bool> belongsToThisSide, CancellationToken ct)
    {
        if (req.Any(s => s.Value < 0 || s.Value > 100))
        {
            return Results.BadRequest(new { message = "Value tiap aspek harus di antara 0 dan 100." });
        }

        var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.PlacementId == placement.Id, ct);
        var rubric = await ResolveRubricAsync(db, assessment, placement.TenantId, ct);
        if (rubric is null)
        {
            return Results.UnprocessableEntity(new { message = "Belum ada rubrik default utk tenant ini - admin harus buat rubrik dulu." });
        }

        var aspectsById = rubric.Aspects.ToDictionary(a => a.Id);
        foreach (var input in req)
        {
            if (!aspectsById.TryGetValue(input.AspectId, out var aspect) || !belongsToThisSide(aspect))
            {
                return Results.BadRequest(new { message = $"AspectId {input.AspectId} bukan aspek sisi ini pada rubrik aktif." });
            }
        }

        if (assessment is null)
        {
            assessment = new Assessment { Id = Guid.NewGuid(), TenantId = placement.TenantId, PlacementId = placement.Id, RubricTemplateId = rubric.Id };
            db.Assessments.Add(assessment);
        }
        else
        {
            // VOK-H3-E3 guard yang sudah ada, dipakai ULANG di sini (bukan re-implementasi) - AC:
            // "revisi skor [setelah final] → 409". Dibiarkan throw (BUKAN try/catch lokal) - pola
            // SAMA persis dgn JournalEndpoints.ApproveJournal - DomainImmutableExceptionHandler
            // (Middleware/, global) yang memetakan ke 409 + body {code,message} konsisten.
            AssessmentImmutabilityGuard.EnsureMutable(assessment);
        }

        foreach (var input in req)
        {
            var existingScore = await db.AssessmentScores.FirstOrDefaultAsync(s => s.AssessmentId == assessment.Id && s.RubricAspectId == input.AspectId, ct);
            if (existingScore is null)
            {
                db.AssessmentScores.Add(new AssessmentScore
                {
                    Id = Guid.NewGuid(), AssessmentId = assessment.Id, RubricAspectId = input.AspectId,
                    ScoredBy = side, ScoredByUserId = scorerUserId, Value = input.Value,
                });
            }
            else
            {
                existingScore.Value = input.Value;
                existingScore.ScoredByUserId = scorerUserId;
            }
        }

        await db.SaveChangesAsync(ct);
        return await BuildAssessmentDtoResultAsync(db, placement.Id, assessment, rubric, ct);
    }

    private static async Task<IResult> GetAssessment(
        Guid placementId, System.Security.Claims.ClaimsPrincipal user, IAuthorizationService authService,
        ITenantContext tenant, VokasiaDbContext db, CancellationToken ct)
    {
        var placement = await db.Placements.FirstOrDefaultAsync(p => p.Id == placementId, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        if (tenant.Role == nameof(UserRole.IndustryMentor))
        {
            var authResult = await authService.AuthorizeAsync(user, placement, RbacPolicies.MentorOwnPlacement);
            if (!authResult.Succeeded)
            {
                return Results.Forbid();
            }
        }
        else if (tenant.Role == nameof(UserRole.Student))
        {
            if (!tenant.UserId.HasValue || !await db.Students.AnyAsync(s => s.Id == placement.StudentId && s.UserId == tenant.UserId, ct))
            {
                return Results.Forbid();
            }
        }
        else if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }
        // Kalau caller punya TenantId, EF global query filter SUDAH membatasi `db.Placements` di
        // atas ke tenant sendiri - placement lintas tenant tak akan pernah ditemukan (404), tak
        // perlu cek ulang TenantId di sini (sama alasan dgn GetPlacement di CompaniesAndPlacements).

        var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.PlacementId == placementId, ct);
        var rubric = await ResolveRubricAsync(db, assessment, placement.TenantId, ct);
        if (rubric is null)
        {
            return Results.NotFound();
        }

        return await BuildAssessmentDtoResultAsync(db, placementId, assessment, rubric, ct);
    }

    private static async Task<IResult> BuildAssessmentDtoResultAsync(VokasiaDbContext db, Guid placementId, Assessment? assessment, RubricTemplate rubric, CancellationToken ct)
    {
        var scores = assessment is null
            ? []
            : await db.AssessmentScores.AsNoTracking().Where(s => s.AssessmentId == assessment.Id).ToListAsync(ct);
        var scoresByAspect = scores.ToDictionary(s => s.RubricAspectId);

        var aspectDtos = rubric.Aspects.Select(a =>
        {
            scoresByAspect.TryGetValue(a.Id, out var score);
            var mentorValue = score is { ScoredBy: ScoredBy.Mentor } ? score.Value : (decimal?)null;
            var teacherValue = score is { ScoredBy: ScoredBy.Teacher } ? score.Value : (decimal?)null;
            return new AssessmentAspectDto(a.Id, a.Name, a.Kind, a.Weight, mentorValue, teacherValue);
        }).ToList();

        var mentorAspects = rubric.Aspects.Where(a => IsMentorSide(a.Kind)).ToList();
        var teacherAspects = rubric.Aspects.Where(a => !IsMentorSide(a.Kind)).ToList();
        var mentorDone = mentorAspects.Count > 0 && mentorAspects.All(a => scoresByAspect.ContainsKey(a.Id));
        var teacherDone = teacherAspects.Count > 0 && teacherAspects.All(a => scoresByAspect.ContainsKey(a.Id));

        var dto = new AssessmentDto(
            assessment?.Id ?? Guid.Empty, placementId, aspectDtos, mentorDone, teacherDone,
            assessment?.FinalScore, assessment?.IsFinal ?? false);

        return Results.Ok(dto);
    }

    private static async Task<IResult> FinalizeAssessment(Guid periodId, FinalizeAssessmentRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var placementsQuery = db.Placements.AsNoTracking().Where(p => p.PeriodId == periodId);
        if (req.PlacementId.HasValue)
        {
            placementsQuery = placementsQuery.Where(p => p.Id == req.PlacementId.Value);
        }
        var placements = await placementsQuery.ToListAsync(ct);

        var finalized = new List<Guid>();
        var incomplete = new List<IncompleteAssessmentDto>();

        foreach (var placement in placements)
        {
            var assessment = await db.Assessments.FirstOrDefaultAsync(a => a.PlacementId == placement.Id, ct);
            if (assessment is { IsFinal: true })
            {
                continue; // idempoten - sudah final sebelumnya, tak diutak-atik/tak dilaporkan ulang.
            }

            var rubric = await ResolveRubricAsync(db, assessment, placement.TenantId, ct);
            if (rubric is null || rubric.Aspects.Count == 0)
            {
                incomplete.Add(new IncompleteAssessmentDto(placement.Id, ["(belum ada rubrik aktif utk tenant)"]));
                continue;
            }

            var scores = assessment is null
                ? new Dictionary<Guid, decimal>()
                : await db.AssessmentScores.AsNoTracking().Where(s => s.AssessmentId == assessment.Id)
                    .ToDictionaryAsync(s => s.RubricAspectId, s => s.Value, ct);

            decimal finalScore;
            try
            {
                finalScore = AssessmentScoring.ComputeWeightedScore(rubric.Aspects, scores);
            }
            catch (AssessmentScoring.IncompleteScoresException ex)
            {
                incomplete.Add(new IncompleteAssessmentDto(placement.Id, ex.MissingAspectNames.ToList()));
                continue;
            }

            if (assessment is null)
            {
                assessment = new Assessment { Id = Guid.NewGuid(), TenantId = placement.TenantId, PlacementId = placement.Id, RubricTemplateId = rubric.Id };
                db.Assessments.Add(assessment);
            }
            // assessment (kalau bukan null) di-fetch via db.Assessments.FirstOrDefaultAsync TANPA
            // AsNoTracking di atas - sudah tracked EF, cukup ubah properti langsung (tak perlu Attach).

            assessment.FinalScore = finalScore;
            assessment.IsFinal = true;
            assessment.FinalizedAt = DateTimeOffset.UtcNow;

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "AssessmentFinalized",
                PayloadJson = JsonSerializer.Serialize(new { assessment.PlacementId, assessment.FinalScore }),
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(), TenantId = tenant.TenantId, ActorUserId = tenant.UserId ?? Guid.Empty,
                Action = "AssessmentFinalized", Entity = nameof(Assessment), EntityId = assessment.Id.ToString(),
                MetaJson = JsonSerializer.Serialize(new { assessment.PlacementId, assessment.FinalScore }),
            });

            finalized.Add(placement.Id);
        }

        await db.SaveChangesAsync(ct);

        var result = new FinalizeAssessmentResult(finalized, incomplete);

        // AC literal: "Given skor belum lengkap, When finalize, Then 422 + daftar yang kurang" -
        // ditegakkan APA ADANYA utk kasus finalize SATU placement spesifik (req.PlacementId
        // terisi). Utk mode BATCH periode penuh (PlacementId null), best-effort: yang lengkap
        // tetap difinalisasi, yang kurang dilaporkan di body yang SAMA (200) - supaya satu siswa
        // telat tak memblokir seluruh angkatan yang sudah selesai (perilaku diputuskan disini,
        // bukan literal tertulis di ticket - didokumentasikan DECISIONS.md D33).
        if (req.PlacementId.HasValue && incomplete.Count > 0)
        {
            return Results.UnprocessableEntity(result);
        }

        return Results.Ok(result);
    }
}
