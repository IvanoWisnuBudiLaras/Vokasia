using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Messaging;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;
using Vokasia.Worker.Jobs;

namespace Vokasia.Tests.Assessment;

/// <summary>
/// VOK-H5-E1 §3 — OpenAssessmentPhase cron. Pola sama persis dgn JournalCronJobsTests (dipanggil
/// langsung via constructor + `runDate` injeksi, Notifier sungguhan bukan mock).
/// </summary>
public class AssessmentCronJobsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public AssessmentCronJobsTests(VokasiaApiFactory factory) => _factory = factory;

    private static AssessmentCronJobs Jobs(VokasiaDbContext db) => new(db, new Notifier(db), NullLogger<AssessmentCronJobs>.Instance);

    private async Task<(VokasiaDbContext Db, Guid PeriodId, Guid PlacementId, Guid MentorUserId, Guid TeacherId)> SeedAsync(
        IServiceScope scope, DateOnly endDate, PeriodStatus status = PeriodStatus.Active)
    {
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var mentorUserId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Cron Assessment", StartDate = endDate.AddMonths(-6), EndDate = endDate, ClassLevels = "XII", Status = status };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = period.Id, TeacherId = teacherId, MentorUserId = mentorUserId, Status = PlacementStatus.Active };

        db.Periods.Add(period);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();
        return (db, period.Id, placement.Id, mentorUserId, teacherId);
    }

    [Fact]
    public async Task OpenAssessmentPhase_PeriodExactlyH14_TransitionsStatusAndNotifiesMentorAndTeacher()
    {
        using var scope = _factory.Services.CreateScope();
        var today = new DateOnly(2026, 7, 1);
        var (db, periodId, _, mentorUserId, teacherId) = await SeedAsync(scope, today.AddDays(14));

        await Jobs(db).OpenAssessmentPhase(today);

        var period = await db.Periods.FirstAsync(p => p.Id == periodId);
        Assert.Equal(PeriodStatus.Assessment, period.Status);
        Assert.Contains(db.Notifications, n => n.UserId == mentorUserId && n.Type == nameof(NotificationType.AssessmentPhaseOpened));
        Assert.Contains(db.Notifications, n => n.UserId == teacherId && n.Type == nameof(NotificationType.AssessmentPhaseOpened));
    }

    [Fact]
    public async Task OpenAssessmentPhase_PeriodNotYetH14_NoChangeNoNotification()
    {
        using var scope = _factory.Services.CreateScope();
        var today = new DateOnly(2026, 7, 1);
        var (db, periodId, _, _, _) = await SeedAsync(scope, today.AddDays(20)); // masih 20 hari lagi, belum H-14.

        await Jobs(db).OpenAssessmentPhase(today);

        var period = await db.Periods.FirstAsync(p => p.Id == periodId);
        Assert.Equal(PeriodStatus.Active, period.Status);
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task OpenAssessmentPhase_CalledTwiceSameDay_Idempotent_NoDuplicateNotification()
    {
        using var scope = _factory.Services.CreateScope();
        var today = new DateOnly(2026, 7, 1);
        var (db, _, _, mentorUserId, _) = await SeedAsync(scope, today.AddDays(14));

        await Jobs(db).OpenAssessmentPhase(today);
        await Jobs(db).OpenAssessmentPhase(today); // re-run kedua (mis. Hangfire retry) - idempoten.

        var mentorNotifCount = db.Notifications.Count(n => n.UserId == mentorUserId && n.Type == nameof(NotificationType.AssessmentPhaseOpened));
        Assert.Equal(1, mentorNotifCount); // BUKAN 2 - status sudah Assessment, run kedua tak temukan periode lagi (filter Status==Active).
    }

    // ---------- EnqueueCertificateBatch (VOK-H5-E1 §5) ----------

    private async Task<(Guid PlacementId, Guid TenantId)> SeedFinalizedAssessmentAsync(IServiceScope scope, DateTimeOffset finalizedAt)
    {
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Completed };
        var assessment = new Vokasia.Domain.Entities.Assessment { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, RubricTemplateId = Guid.NewGuid(), IsFinal = true, FinalScore = 88m, FinalizedAt = finalizedAt };
        db.Placements.Add(placement);
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();
        return (placement.Id, tenantId);
    }

    [Fact]
    public async Task EnqueueCertificateBatch_FinalizedYesterdayJakartaTime_EnqueuesCertificateRequested()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var today = new DateOnly(2026, 7, 15);
        // 2026-07-14 23:00 UTC = 2026-07-15 06:00 WIB (UTC+7) -> HARI INI di Jakarta, BUKAN kemarin.
        // Dipakai nilai yg jelas2 "kemarin siang WIB" spy tak ambigu lintas tengah malam:
        // 2026-07-14 03:00 UTC = 2026-07-14 10:00 WIB -> kemarin, jelas.
        var finalizedYesterdayWib = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);
        var (placementId, tenantId) = await SeedFinalizedAssessmentAsync(scope, finalizedYesterdayWib);

        await Jobs(db).EnqueueCertificateBatch(today);

        var outboxRow = await db.OutboxMessages.FirstOrDefaultAsync(o => o.Type == "CertificateRequested" && o.PayloadJson.Contains(placementId.ToString()));
        Assert.NotNull(outboxRow);
    }

    [Fact]
    public async Task EnqueueCertificateBatch_FinalizedTodayNotYesterday_DoesNotEnqueue()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var today = new DateOnly(2026, 7, 15);
        var finalizedTodayWib = new DateTimeOffset(2026, 7, 15, 3, 0, 0, TimeSpan.Zero); // 10:00 WIB HARI INI, bukan kemarin.
        var (placementId, _) = await SeedFinalizedAssessmentAsync(scope, finalizedTodayWib);

        await Jobs(db).EnqueueCertificateBatch(today);

        var outboxRow = await db.OutboxMessages.FirstOrDefaultAsync(o => o.Type == "CertificateRequested" && o.PayloadJson.Contains(placementId.ToString()));
        Assert.Null(outboxRow);
    }

    [Fact]
    public async Task EnqueueCertificateBatch_AlreadyHasCertificate_Idempotent_DoesNotEnqueueAgain()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var today = new DateOnly(2026, 7, 15);
        var finalizedYesterdayWib = new DateTimeOffset(2026, 7, 14, 3, 0, 0, TimeSpan.Zero);
        var (placementId, tenantId) = await SeedFinalizedAssessmentAsync(scope, finalizedYesterdayWib);
        db.Certificates.Add(new Certificate { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, CertCode = Vokasia.Domain.Common.CertCodeGenerator.Generate(), PdfKey = "already/exists.pdf" });
        await db.SaveChangesAsync();

        await Jobs(db).EnqueueCertificateBatch(today);

        var outboxRow = await db.OutboxMessages.FirstOrDefaultAsync(o => o.Type == "CertificateRequested" && o.PayloadJson.Contains(placementId.ToString()));
        Assert.Null(outboxRow);
    }
}
