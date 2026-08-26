using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Api.Validation;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H4-E1 §4 GetSchoolDashboard — layar W3. Baca proyeksi StudentDailyStatus (ditulis
/// JournalSubmittedConsumer/StreakCounterConsumer/FlagGhostingStudents, H4-E1), BUKAN hitung ulang
/// dari JournalEntries mentah tiap request - itulah tujuan proyeksi ada sejak awal (lihat
/// doc-comment StudentDailyStatus, JournalEntities.cs: "sumber baca cepat dashboard W3... BUKAN
/// dihitung ulang saat baca").
///
/// "SATU query agregat" (AC ticket) ditafsirkan sbg SEMANGAT-nya: tak ada N+1 PER SISWA (900 siswa
/// tak memicu 900 query terpisah) - beberapa query O(1) terpisah (total hari ini, pending approvals,
/// daftar flagged) tetap dalam batas wajar krn masing2 ditopang index
/// (StudentDailyStatus{PeriodId,Date,Rag} sudah dibuat khusus utk ini sejak VokasiaDbContext H3-E1).
/// </summary>
public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/school/{periodId:guid}", GetSchoolDashboard)
            .WithTags("Dashboard")
            .RequireAuthorization(RbacPolicies.TenantMember);

        var studentHome = app.MapGroup("/api/students/me/home")
            .WithTags("Student Portal")
            .AddEndpointFilter<ValidationFilter>()
            .RequireAuthorization(RbacPolicies.StudentSelf);
        studentHome.MapGet("", GetMyHome);

        return app;
    }

    private static async Task<IResult> GetSchoolDashboard(Guid periodId, VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var teacherScoped = string.Equals(tenant.Role, UserRole.Teacher.ToString(), StringComparison.OrdinalIgnoreCase);
        if (teacherScoped && !tenant.UserId.HasValue)
        {
            return Results.Forbid();
        }

        var today = AppTimeZone.TodayJakarta();

        var todaySlotStatuses = await db.JournalSlots.AsNoTracking()
            .Where(s => s.Date == today)
            .Join(
                db.Placements.AsNoTracking().Where(p => p.PeriodId == periodId && p.Status == PlacementStatus.Active &&
                    (!teacherScoped || p.TeacherId == tenant.UserId)),
                s => s.PlacementId, p => p.Id, (s, _) => s.Status)
            .ToListAsync(ct);

        var totalToday = todaySlotStatuses.Count;
        var filledToday = todaySlotStatuses.Count(s => s == JournalSlotStatus.Filled);
        // Tak ada slot hari ini (akhir pekan/libur, atau cron 05:00 belum jalan) -> 0%, bukan NaN.
        var journalTodayPct = totalToday == 0 ? 0.0 : Math.Round(100.0 * filledToday / totalToday, 1);

        var pendingApprovals = await db.JournalEntries.AsNoTracking()
            .Where(e => e.Status == JournalEntryStatus.Submitted)
            .Join(db.Placements.AsNoTracking().Where(p => p.PeriodId == periodId &&
                (!teacherScoped || p.TeacherId == tenant.UserId)), e => e.PlacementId, p => p.Id, (e, _) => e.Id)
            .CountAsync(ct);

        // [GAP dicatat, bukan diam-diam dihitung asal2an - lihat DECISIONS.md]: Visit
        // (VisitEntities.cs) HANYA merekam kunjungan yang SUDAH terjadi (Date/Notes/PhotoKey/
        // SignatureKey) - tak ada kolom jadwal/tenggat/status sama sekali, "terlambat" tak punya
        // definisi apa pun dari skema yang ada saat ini (kemungkinan fitur penjadwalan kunjungan
        // ada di ticket lain, H5). 0 = placeholder jujur, bukan angka dikarang.
        const int lateVisits = 0;

        var flagged = await db.StudentDailyStatuses.AsNoTracking()
            .Where(x => x.PeriodId == periodId && x.Date == today && x.Rag != RagStatus.Green)
            .Join(db.Students.AsNoTracking(), x => x.StudentId, s => s.Id, (x, s) => new { x.Rag, StudentId = s.Id, s.FullName })
            .Join(
                db.Placements.AsNoTracking().Where(p => p.PeriodId == periodId &&
                    (!teacherScoped || p.TeacherId == tenant.UserId)),
                x => x.StudentId, p => p.StudentId, (x, p) => new { x.Rag, x.StudentId, x.FullName, p.CompanyId })
            .Join(db.Companies.AsNoTracking(), x => x.CompanyId, c => c.Id, (x, c) => new DashboardFlaggedStudentDto(
                x.StudentId, x.FullName, c.Name, x.Rag,
                x.Rag == RagStatus.Red ? "≥ 3 hari kerja tanpa jurnal" : "1-2 hari kerja tanpa jurnal"))
            .ToListAsync(ct);

        return Results.Ok(new SchoolDashboardDto(journalTodayPct, pendingApprovals, lateVisits, flagged));
    }

    private static async Task<IResult> GetMyHome(VokasiaDbContext db, ITenantContext tenant, CancellationToken ct)
    {
        if (!tenant.UserId.HasValue || !tenant.TenantId.HasValue)
        {
            return Results.Forbid();
        }

        var student = await db.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == tenant.UserId.Value, ct);
        if (student is null)
        {
            return Results.NotFound();
        }

        var placement = await db.Placements.AsNoTracking()
            .Where(p => p.StudentId == student.Id)
            .OrderByDescending(p => p.Status == PlacementStatus.Active)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (placement is null)
        {
            return Results.NotFound();
        }

        var companyName = await db.Companies.AsNoTracking()
            .Where(c => c.Id == placement.CompanyId)
            .Select(c => c.Name)
            .FirstOrDefaultAsync(ct) ?? "Belum ditentukan";
        var periodName = await db.Periods.AsNoTracking()
            .Where(p => p.Id == placement.PeriodId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct) ?? "Belum ditentukan";
        var teacherName = await db.Users.AsNoTracking()
            .Where(u => u.Id == placement.TeacherId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
        var mentorName = placement.MentorUserId.HasValue
            ? await db.Users.AsNoTracking()
                .Where(u => u.Id == placement.MentorUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct)
            : null;

        var revisionItems = await db.JournalEntries.AsNoTracking()
            .Where(e => e.PlacementId == placement.Id && e.Status == JournalEntryStatus.Rejected)
            .OrderByDescending(e => e.SubmittedAt)
            .Take(3)
            .Select(e => new StudentHomeRevisionDto(e.Id, e.SubmittedAt, e.MentorNote))
            .ToListAsync(ct);

        return Results.Ok(new StudentHomeDto(
            placement.Status,
            companyName,
            periodName,
            mentorName,
            teacherName,
            PlacementReady: true,
            JournalActive: await db.JournalSlots.AnyAsync(s => s.PlacementId == placement.Id, ct),
            AssessmentStarted: await db.Assessments.AnyAsync(a => a.PlacementId == placement.Id, ct) ||
                               await db.LearningAssessments.AnyAsync(a => a.PlacementId == placement.Id, ct),
            CertificateIssued: await db.Certificates.AnyAsync(c => c.PlacementId == placement.Id, ct),
            revisionItems));
    }
}
