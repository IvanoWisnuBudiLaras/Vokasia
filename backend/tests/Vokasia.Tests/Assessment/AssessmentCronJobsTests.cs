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
}
