using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Vokasia.Api.Auth;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>VOK-H2-E1 §5: link/propose DUDI ke tenant, kuota slot, dan placement.</summary>
public static class CompaniesAndPlacementsEndpoints
{
    public static IEndpointRouteBuilder MapCompaniesAndPlacementsEndpoints(this IEndpointRouteBuilder app)
    {
        // VOK-H3-E3 §2: ValidationFilter global (ProposeCompanyValidator, CreatePlacementValidator).
        var companies = app.MapGroup("/api/companies").WithTags("Companies").AddEndpointFilter<ValidationFilter>();
        companies.MapPost("/link/{companyId:guid}", LinkCompanyToTenant).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        companies.MapPost("/propose", ProposeCompany).RequireAuthorization(RbacPolicies.TenantAdminOnly);
        companies.MapGet("/", ListTenantCompanies).RequireAuthorization(RbacPolicies.TenantMember);
        companies.MapPost("/{companyId:guid}/periods/{periodId:guid}/slots", SetCompanySlots).RequireAuthorization(RbacPolicies.DeptHeadPlus);

        var placements = app.MapGroup("/api/placements").WithTags("Placements").AddEndpointFilter<ValidationFilter>();
        placements.MapPost("/", CreatePlacement).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        placements.MapPost("/bulk", BulkCreatePlacements).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        placements.MapPut("/{id:guid}/teacher/{teacherId:guid}", AssignTeacher).RequireAuthorization(RbacPolicies.DeptHeadPlus);
        placements.MapGet("/", ListPlacements).RequireAuthorization();
        placements.MapGet("/{id:guid}", GetPlacement).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LinkCompanyToTenant(Guid companyId, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var exists = await db.Companies.AnyAsync(c => c.Id == companyId, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        var alreadyLinked = await db.TenantCompanies.AnyAsync(tc => tc.TenantId == tenant.TenantId && tc.CompanyId == companyId, ct);
        if (!alreadyLinked)
        {
            db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.TenantId.Value, CompanyId = companyId });
            await db.SaveChangesAsync(ct);
        }

        return Results.NoContent();
    }

    private static async Task<IResult> ListTenantCompanies(VokasiaDbContext db, CancellationToken ct)
    {
        var rows = await (from link in db.TenantCompanies.AsNoTracking()
                          join company in db.Companies.AsNoTracking() on link.CompanyId equals company.Id
                          orderby company.Name
                          select company).ToListAsync(ct);
        return Results.Ok(rows.Select(ToDto).ToList());
    }

    private static async Task<IResult> ProposeCompany(ProposeCompanyRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Sector = req.Sector,
            City = req.City,
            Address = req.Address,
            ContactPerson = req.ContactPerson,
            IsVerified = false, // verifikasi oleh SuperAdmin, H6-E1.
        };
        db.Companies.Add(company);

        if (tenant.TenantId.HasValue)
        {
            db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.TenantId.Value, CompanyId = company.Id });
        }

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/companies/{company.Id}", ToDto(company));
    }

    private static async Task<IResult> SetCompanySlots(
        Guid companyId, Guid periodId, [FromBody] int slots, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var slot = await db.CompanySlots.FirstOrDefaultAsync(
            s => s.TenantId == tenant.TenantId && s.CompanyId == companyId && s.PeriodId == periodId, ct);

        if (slot is null)
        {
            db.CompanySlots.Add(new CompanySlot { Id = Guid.NewGuid(), TenantId = tenant.TenantId.Value, CompanyId = companyId, PeriodId = periodId, Slots = slots });
        }
        else
        {
            slot.Slots = slots;
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>
    /// VOK-H6-E1 §5 (FR-BIL-03) — AKTIF (ganti stub H2-E1): hitung placement AKTIF tenant vs
    /// Plan.MaxPlacements. Tenant tanpa PlanId (belum pernah diberi paket) = TANPA BATAS (ASSUMPTION
    /// MVP, pola sama TryReserveSlot "belum ada kuota diset = tanpa batas") — bukan diam-diam
    /// menolak semua placement tenant yang PlanId-nya null.
    /// </summary>
    private static async Task CheckQuotaOnPlacementAsync(VokasiaDbContext db, Guid tenantId, CancellationToken ct, int pendingReservations = 0)
    {
        var planId = await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.PlanId).FirstOrDefaultAsync(ct);
        if (!planId.HasValue)
        {
            return;
        }

        var maxPlacements = await db.Plans.AsNoTracking().Where(p => p.Id == planId.Value).Select(p => (int?)p.MaxPlacements).FirstOrDefaultAsync(ct);
        if (!maxPlacements.HasValue)
        {
            return;
        }

        var activeCount = await db.Placements.AsNoTracking().CountAsync(p => p.TenantId == tenantId && p.Status == PlacementStatus.Active, ct);
        if (activeCount + pendingReservations >= maxPlacements.Value)
        {
            throw new QuotaExceededException($"Kuota placement aktif ({maxPlacements.Value}) sudah tercapai sesuai plan tenant — upgrade plan atau nonaktifkan placement lama.");
        }
    }

    private static async Task<(bool Ok, string? Error)> TryReserveSlot(VokasiaDbContext db, Guid companyId, Guid periodId, CancellationToken ct)
    {
        var slot = await db.CompanySlots.AsNoTracking().FirstOrDefaultAsync(s => s.CompanyId == companyId && s.PeriodId == periodId, ct);
        if (slot is null)
        {
            return (true, null); // belum ada kuota diset = tanpa batas (ASSUMPTION MVP).
        }

        var used = await db.Placements.CountAsync(p => p.CompanyId == companyId && p.PeriodId == periodId && p.Status == PlacementStatus.Active, ct);
        return used >= slot.Slots ? (false, "Slot DUDI untuk periode ini sudah penuh.") : (true, null);
    }

    /// <summary>VOK-H6-E1 §1 AC (DeactivateTenant): "placement baru terblokir" — tenant nonaktif tak boleh bikin placement baru (data lama tetap terbaca, hanya create yang ditolak).</summary>
    private static async Task<bool> TenantIsActiveAsync(VokasiaDbContext db, Guid tenantId, CancellationToken ct) =>
        await db.Tenants.AsNoTracking().Where(t => t.Id == tenantId).Select(t => t.IsActive).FirstOrDefaultAsync(ct);

    private static async Task<IResult> CreatePlacement(CreatePlacementRequest req, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        await using var transaction = await BeginSerializableQuotaTransactionAsync(db, ct);
        try
        {
            if (!await TenantIsActiveAsync(db, tenant.TenantId.Value, ct))
            {
                return Results.Conflict(new { message = "Tenant nonaktif — tidak bisa membuat placement baru." });
            }

            var (ok, error) = await TryReserveSlot(db, req.CompanyId, req.PeriodId, ct);
            if (!ok)
            {
                return Results.Conflict(new { message = error });
            }

            await CheckQuotaOnPlacementAsync(db, tenant.TenantId.Value, ct);

            var placement = new Placement
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId.Value,
                StudentId = req.StudentId,
                CompanyId = req.CompanyId,
                PeriodId = req.PeriodId,
                TeacherId = req.TeacherId,
                MentorEmail = req.MentorEmail,
                Status = PlacementStatus.Active,
            };
            db.Placements.Add(placement);

            // AC: OutboxMessage{PlacementCreated} tercatat 1 transaksi dgn placement (dispatcher nyata H4-E1).
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = "PlacementCreated",
                PayloadJson = JsonSerializer.Serialize(new { placement.Id, placement.StudentId, placement.CompanyId, placement.PeriodId, placement.MentorEmail }),
            });

            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return Results.Created($"/api/placements/{placement.Id}", ToDto(placement));
        }
        catch (Exception ex) when (IsQuotaConcurrencyConflict(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            db.ChangeTracker.Clear();
            return Results.Conflict(new { message = "Kuota placement berubah bersamaan. Muat ulang lalu coba lagi." });
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            throw;
        }
    }

    private static async Task<IResult> BulkCreatePlacements(List<CreatePlacementRequest> reqs, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        await using var transaction = await BeginSerializableQuotaTransactionAsync(db, ct);
        try
        {
            if (!await TenantIsActiveAsync(db, tenant.TenantId.Value, ct))
            {
                return Results.Conflict(new { message = "Tenant nonaktif — tidak bisa membuat placement baru." });
            }

            var successIds = new List<Guid>();
            var errors = new List<ImportRowError>();
            var pendingReservations = 0;

            for (var i = 0; i < reqs.Count; i++)
            {
                var req = reqs[i];
                try
                {
                    await CheckQuotaOnPlacementAsync(db, tenant.TenantId.Value, ct, pendingReservations);
                }
                catch (QuotaExceededException ex)
                {
                    errors.Add(new ImportRowError(i, "TenantId", ex.Message));
                    continue;
                }

                var (ok, error) = await TryReserveSlot(db, req.CompanyId, req.PeriodId, ct);
                if (!ok)
                {
                    errors.Add(new ImportRowError(i, "CompanyId", error!));
                    continue;
                }

                var placement = new Placement
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.TenantId.Value,
                    StudentId = req.StudentId,
                    CompanyId = req.CompanyId,
                    PeriodId = req.PeriodId,
                    TeacherId = req.TeacherId,
                    MentorEmail = req.MentorEmail,
                    Status = PlacementStatus.Active,
                };
                db.Placements.Add(placement);
                db.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = "PlacementCreated",
                    PayloadJson = JsonSerializer.Serialize(new { placement.Id, placement.StudentId, placement.CompanyId, placement.PeriodId }),
                });
                successIds.Add(placement.Id);
                pendingReservations++;
            }

            await db.SaveChangesAsync(ct);
            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            return Results.Ok(new BulkResult(successIds, errors));
        }
        catch (Exception ex) when (IsQuotaConcurrencyConflict(ex))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            db.ChangeTracker.Clear();
            return Results.Conflict(new { message = "Kuota placement berubah bersamaan. Muat ulang lalu coba lagi." });
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            throw;
        }
    }

    private static async Task<IDbContextTransaction?> BeginSerializableQuotaTransactionAsync(VokasiaDbContext db, CancellationToken ct)
    {
        if (!db.Database.IsRelational())
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
    }

    private static bool IsQuotaConcurrencyConflict(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            return true;
        }

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres &&
                postgres.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<IResult> AssignTeacher(Guid id, Guid teacherId, VokasiaDbContext db, CancellationToken ct)
    {
        var placement = await db.Placements.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        placement.TeacherId = teacherId;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDto(placement));
    }

    private static async Task<IResult> ListPlacements(
        VokasiaDbContext db, ITenantContext tenant, CancellationToken ct,
        [FromQuery] Guid periodId, [FromQuery] Guid? companyId = null, [FromQuery] PlacementStatus? status = null,
        [FromQuery] Guid? teacherId = null, [FromQuery] Guid? studentId = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!tenant.UserId.HasValue || (tenant.Role != nameof(UserRole.Student) && !tenant.TenantId.HasValue))
        {
            return Results.Forbid();
        }

        var query = db.Placements.AsNoTracking().Where(p => p.PeriodId == periodId);
        if (tenant.Role == nameof(UserRole.Student))
        {
            query = query.Where(p => db.Students.Any(s => s.Id == p.StudentId && s.UserId == tenant.UserId));
        }
        else if (tenant.Role == nameof(UserRole.Teacher))
        {
            query = query.Where(p => p.TeacherId == tenant.UserId);
        }
        else if (tenant.Role == nameof(UserRole.IndustryMentor))
        {
            query = query.Where(p => p.MentorUserId == tenant.UserId);
        }
        if (companyId.HasValue)
        {
            query = query.Where(p => p.CompanyId == companyId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        // VOK-H4-E2 §"halaman guru bimbingan" — GAP ditemukan: endpoint ini (H2-E1) tak pernah
        // punya filter per-guru sama sekali (cocok drpd dgn gap serupa D16 utk mentor: "ListPlacements
        // wajib periodId, tanpa filter studentId/mentorUserId"). Filter TAMBAHAN opsional (tak
        // mengubah perilaku pemanggil lama tanpa teacherId) - FE kirim teacherId=session.id (=
        // AppUser.Id sendiri, sesuai Placement.TeacherId == AppUser.Id langsung, lihat komentar
        // PlacementCreatedConsumer) utk dapat "siswa bimbinganku". TIDAK ada resource-based
        // authorization tambahan di sini (siapa pun boleh KIRIM teacherId siapa saja) - aman krn
        // scope keamanan SUNGGUHAN ada di endpoint lain (mis. AddTeacherComment tetap TeacherPlus)
        // dan data placement per-tenant bukan rahasia antar staf sekolah yang sama (AGENTS.md #2
        // RBAC ditegakkan per aksi, bukan per row-read staf internal) - beda dgn data siswa publik/
        // portofolio yang memang privasi-sensitif (H6+).
        if (teacherId.HasValue)
        {
            query = query.Where(p => p.TeacherId == teacherId.Value);
        }

        // VOK-H4-E2 §"StudentDetailDrawer" dashboard W3 — sama alasan dgn teacherId di atas: drawer
        // detail siswa cuma punya studentId (dari DashboardFlaggedStudentDto) + periodId (dari
        // selector), butuh cari placement-nya utk lanjut panggil GET /journals/for-teacher/{id}.
        if (studentId.HasValue)
        {
            query = query.Where(p => p.StudentId == studentId.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(p => ToDto(p)).ToListAsync(ct);

        return Results.Ok(new Paged<PlacementDto>(items, page, pageSize, total));
    }

    private static async Task<IResult> GetPlacement(Guid id, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        var placement = await db.Placements.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (placement is not null && tenant.Role == nameof(UserRole.Student) &&
            (!tenant.UserId.HasValue || !await db.Students.AnyAsync(s => s.Id == placement.StudentId && s.UserId == tenant.UserId, ct)))
        {
            return Results.NotFound();
        }
        if (placement is not null && tenant.Role == nameof(UserRole.Teacher) && placement.TeacherId != tenant.UserId)
        {
            return Results.NotFound();
        }
        if (placement is not null && tenant.Role == nameof(UserRole.IndustryMentor) && placement.MentorUserId != tenant.UserId)
        {
            return Results.NotFound();
        }
        return placement is null ? Results.NotFound() : Results.Ok(ToDto(placement));
    }

    private static CompanyDto ToDto(Company c) => new(c.Id, c.Name, c.Sector, c.City, c.Address, c.ContactPerson, c.IsVerified, c.MergedIntoId);
    private static PlacementDto ToDto(Placement p) => new(p.Id, p.StudentId, p.CompanyId, p.PeriodId, p.TeacherId, p.MentorUserId, p.Status);
}
