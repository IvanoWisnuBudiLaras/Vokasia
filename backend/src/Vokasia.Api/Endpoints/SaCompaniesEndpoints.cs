using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H6-E1 §2 — /sa/companies: registry DUDI GLOBAL (lintas tenant, nilai jual utama Vokasia,
/// FR-SA-02). Semua endpoint policy SaOnly. ProposeCompany (tenant-facing, H2-E1) TETAP ADA terpisah
/// — endpoint di sini adalah sisi SA (verifikasi, merge, registry penuh), bukan pengganti.
/// </summary>
public static class SaCompaniesEndpoints
{
    public static IEndpointRouteBuilder MapSaCompaniesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sa/companies").WithTags("SaCompanies")
            .RequireAuthorization(RbacPolicies.SaOnly)
            .AddEndpointFilter<ValidationFilter>();

        group.MapPost("/", CreateCompany);
        group.MapGet("/{id:guid}", GetCompany);
        group.MapPost("/{id:guid}/verify", VerifyCompany);
        group.MapPost("/merge", MergeCompanies);
        group.MapGet("/", ListCompanies);
        group.MapGet("/search", SearchCompanies);

        return app;
    }

    private static async Task<IResult> CreateCompany(CreateCompanyRequest req, VokasiaDbContext db, CancellationToken ct)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Sector = req.Sector,
            City = req.City,
            Address = req.Address,
            ContactPerson = req.ContactPerson,
            IsVerified = false,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/sa/companies/{company.Id}", ToDto(company));
    }

    /// <summary>AC: "GET company A -> redirect/flag merged" (pasca-merge) — di sini "flag" (MergedIntoId non-null di body 200), bukan redirect HTTP (lebih sederhana utk konsumen API/FE, tetap membuktikan AC: caller TAHU company sudah digabung & ke mana).</summary>
    private static async Task<IResult> GetCompany(Guid id, VokasiaDbContext db, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return company is null ? Results.NotFound() : Results.Ok(ToDto(company));
    }

    private static async Task<IResult> VerifyCompany(Guid id, VokasiaDbContext db, ITenantContext actingUser, CancellationToken ct)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company is null)
        {
            return Results.NotFound();
        }

        company.IsVerified = true;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = null,
            ActorUserId = actingUser.UserId ?? Guid.Empty,
            Action = "CompanyVerified",
            Entity = nameof(Company),
            EntityId = company.Id.ToString(),
            MetaJson = "{}",
        });

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(company));
    }

    /// <summary>
    /// AC: "placement pindah, riwayat tercatat, GET company A -> redirect/flag merged." Dedup:
    /// TenantCompany yang tenant-nya SUDAH linked ke target dibuang (bukan dipindah dobel — PK
    /// komposit {TenantId,CompanyId} akan bentrok kalau dipaksa). Seluruh read-check-write dibungkus
    /// transaksi serializable agar dua merge bersamaan tidak menghasilkan history/target ganda.
    /// </summary>
    private static async Task<IResult> MergeCompanies(MergeCompaniesRequest req, VokasiaDbContext db, ITenantContext actingUser, CancellationToken ct)
    {
        if (req.SourceId == req.TargetId)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["TargetId"] = ["SourceId dan TargetId tidak boleh sama."] });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct);

        try
        {
            var source = await db.Companies.FirstOrDefaultAsync(c => c.Id == req.SourceId, ct);
            var target = await db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.TargetId, ct);
            if (source is null || target is null)
            {
                return Results.NotFound();
            }

            if (source.MergedIntoId.HasValue)
            {
                return Results.Conflict(new { message = "Company sumber sudah pernah di-merge sebelumnya." });
            }

            if (target.MergedIntoId.HasValue)
            {
                return Results.Conflict(new { message = "Company target sudah pernah di-merge sebelumnya." });
            }

            var snapshot = JsonSerializer.Serialize(new { source.Id, source.Name, source.Sector, source.City, source.Address, source.ContactPerson, source.IsVerified });

            // Endpoint ini lintas tenant dan SaOnly. IgnoreQueryFilters dibuat eksplisit supaya merge
            // selalu memindahkan keseluruhan relasi global, terlepas dari ambient tenant request.
            var sourceLinks = await db.TenantCompanies.IgnoreQueryFilters()
                .Where(tc => tc.CompanyId == req.SourceId)
                .ToListAsync(ct);
            var targetLinkedTenantIds = (await db.TenantCompanies.AsNoTracking().IgnoreQueryFilters()
                .Where(tc => tc.CompanyId == req.TargetId)
                .Select(tc => tc.TenantId)
                .ToListAsync(ct))
                .ToHashSet();

            var sourceSlots = await db.CompanySlots.IgnoreQueryFilters()
                .Where(s => s.CompanyId == req.SourceId)
                .ToListAsync(ct);
            var targetSlotKeys = (await db.CompanySlots.AsNoTracking().IgnoreQueryFilters()
                .Where(s => s.CompanyId == req.TargetId)
                .Select(s => new { s.TenantId, s.PeriodId })
                .ToListAsync(ct))
                .Select(s => (s.TenantId, s.PeriodId))
                .ToHashSet();

            if (sourceSlots.Any(s => targetSlotKeys.Contains((s.TenantId, s.PeriodId))))
            {
                return Results.Conflict(new
                {
                    message = "Company target sudah memiliki kuota slot untuk tenant dan periode yang sama.",
                });
            }

            var movedTenantCompanies = 0;
            foreach (var link in sourceLinks)
            {
                db.TenantCompanies.Remove(link);
                if (targetLinkedTenantIds.Add(link.TenantId))
                {
                    db.TenantCompanies.Add(new TenantCompany { TenantId = link.TenantId, CompanyId = req.TargetId, LinkedAt = link.LinkedAt });
                    movedTenantCompanies++;
                }
            }

            foreach (var slot in sourceSlots)
            {
                slot.CompanyId = req.TargetId;
            }

            var placements = await db.Placements.IgnoreQueryFilters()
                .Where(p => p.CompanyId == req.SourceId)
                .ToListAsync(ct);
            foreach (var p in placements)
            {
                p.CompanyId = req.TargetId;
            }

            source.MergedIntoId = req.TargetId;

            db.CompanyMergeHistories.Add(new CompanyMergeHistory
            {
                Id = Guid.NewGuid(),
                SourceCompanyId = req.SourceId,
                TargetCompanyId = req.TargetId,
                SourceSnapshotJson = snapshot,
                MergedByUserId = actingUser.UserId ?? Guid.Empty,
            });

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = null,
                ActorUserId = actingUser.UserId ?? Guid.Empty,
                Action = "CompanyMerged",
                Entity = nameof(Company),
                EntityId = req.SourceId.ToString(),
                MetaJson = JsonSerializer.Serialize(new { req.TargetId }),
            });

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return Results.Ok(new MergeResultDto(req.SourceId, req.TargetId, movedTenantCompanies, placements.Count));
        }
        catch (Exception ex) when (IsMergeConcurrencyConflict(ex))
        {
            db.ChangeTracker.Clear();
            return Results.Conflict(new
            {
                message = "Data company berubah saat proses merge. Muat ulang lalu coba lagi.",
            });
        }
    }

    private static bool IsMergeConcurrencyConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return true;
        }

        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is not PostgresException postgres)
            {
                continue;
            }

            if (postgres.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }

            return postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                   postgres.ConstraintName is
                       "PK_TenantCompanies" or
                       "IX_CompanySlots_TenantId_CompanyId_PeriodId";
        }

        return false;
    }

    private static async Task<IResult> ListCompanies(
        VokasiaDbContext db, CancellationToken ct,
        [FromQuery] string? search = null, [FromQuery] bool? verified = null, [FromQuery] string? city = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.Companies.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Name.Contains(search));
        }

        if (verified.HasValue)
        {
            query = query.Where(c => c.IsVerified == verified.Value);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(c => c.City == city);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(c => c.Name).Skip((page - 1) * pageSize).Take(pageSize).Select(c => ToDto(c)).ToListAsync(ct);

        return Results.Ok(new Paged<CompanyDto>(items, page, pageSize, total));
    }

    /// <summary>AC: "autocomplete linking tenant" — ringan (Id+Name+City saja), TIDAK termasuk company yang sudah merged (MergedIntoId != null) - tenant baru tak boleh link ke company "hantu" yang sudah digabung.</summary>
    private static async Task<IResult> SearchCompanies(VokasiaDbContext db, CancellationToken ct, [FromQuery] string q = "", [FromQuery] int limit = 10)
    {
        var boundedLimit = Math.Clamp(limit, 1, 50);
        var items = await db.Companies.AsNoTracking()
            .Where(c => c.MergedIntoId == null && (string.IsNullOrEmpty(q) || c.Name.Contains(q)))
            .OrderBy(c => c.Name)
            .Take(boundedLimit)
            .Select(c => new CompanySearchDto(c.Id, c.Name, c.City))
            .ToListAsync(ct);

        return Results.Ok(items);
    }

    private static CompanyDto ToDto(Company c) => new(c.Id, c.Name, c.Sector, c.City, c.Address, c.ContactPerson, c.IsVerified, c.MergedIntoId);
}
