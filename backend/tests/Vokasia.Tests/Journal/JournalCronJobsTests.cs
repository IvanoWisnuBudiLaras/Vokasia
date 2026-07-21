using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;
using Vokasia.Worker.Jobs;

namespace Vokasia.Tests.Journal;

/// <summary>
/// AC VOK-H3-E1 §1 (cron Hangfire). Dipanggil LANGSUNG via constructor + <c>runDate</c> injeksi
/// (bukan lewat Hangfire scheduler sungguhan — jadwal 05:00/19:00 WIB & registrasi Hangfire itu
/// sendiri di luar jangkauan test in-process ini, cukup dibuktikan lewat build+`docker compose`
/// Worker jalan; test ini membuktikan LOGIKA job-nya, sesuai izin ticket sendiri "param runDate
/// untuk test/backfill"). Reuse <see cref="VokasiaApiFactory"/> HANYA utk dapat scope+DbContext
/// InMemory yang konsisten dgn suite lain — tidak butuh web host Api sungguhan sama sekali di sini.
/// </summary>
public class JournalCronJobsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public JournalCronJobsTests(VokasiaApiFactory factory) => _factory = factory;

    private static readonly DateOnly Monday = new(2026, 7, 20); // Senin — cek kalender: 2026-07-20 = Senin.
    private static readonly DateOnly Saturday = new(2026, 7, 18);

    private async Task<(VokasiaDbContext Db, Guid PlacementId, Guid PeriodId)> SeedActivePlacementAsync(
        DateOnly periodStart, DateOnly periodEnd, IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var period = new Period
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Periode Uji Cron",
            StartDate = periodStart,
            EndDate = periodEnd,
            ClassLevels = "XII",
            Status = PeriodStatus.Active,
        };
        var placement = new Placement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StudentId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PeriodId = period.Id,
            TeacherId = Guid.NewGuid(),
            Status = PlacementStatus.Active,
        };
        db.Periods.Add(period);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();
        return (db, placement.Id, period.Id);
    }

    private static JournalCronJobs Jobs(VokasiaDbContext db) => new(db, NullLogger<JournalCronJobs>.Instance);

    [Fact]
    public async Task GenerateDailyJournalSlots_Weekend_CreatesNoSlots()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, _) = await SeedActivePlacementAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), scope);

        await Jobs(db).GenerateDailyJournalSlots(Saturday);

        Assert.Empty(db.JournalSlots.Where(s => s.PlacementId == placementId));
    }

    [Fact]
    public async Task GenerateDailyJournalSlots_ActivePlacementWithinPeriod_CreatesOneSlot()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, _) = await SeedActivePlacementAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), scope);

        await Jobs(db).GenerateDailyJournalSlots(Monday);

        var slot = Assert.Single(db.JournalSlots.Where(s => s.PlacementId == placementId));
        Assert.Equal(Monday, slot.Date);
        Assert.Equal(JournalSlotStatus.Empty, slot.Status);
    }

    [Fact]
    public async Task GenerateDailyJournalSlots_CalledTwiceSameDate_IsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, _) = await SeedActivePlacementAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), scope);

        var jobs = Jobs(db);
        await jobs.GenerateDailyJournalSlots(Monday);
        await jobs.GenerateDailyJournalSlots(Monday);

        Assert.Single(db.JournalSlots.Where(s => s.PlacementId == placementId));
    }

    [Fact]
    public async Task GenerateDailyJournalSlots_HolidayDate_SkipsPlacement()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, periodId) = await SeedActivePlacementAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), scope);
        db.Holidays.Add(new Holiday { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), PeriodId = periodId, Date = Monday, Label = "Libur Uji" });
        await db.SaveChangesAsync();

        await Jobs(db).GenerateDailyJournalSlots(Monday);

        Assert.Empty(db.JournalSlots.Where(s => s.PlacementId == placementId));
    }

    [Fact]
    public async Task GenerateDailyJournalSlots_DateOutsidePeriodRange_SkipsPlacement()
    {
        using var scope = _factory.Services.CreateScope();
        // Periode berakhir SEBELUM Monday -> Monday di luar rentang.
        var (db, placementId, _) = await SeedActivePlacementAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1), scope);

        await Jobs(db).GenerateDailyJournalSlots(Monday);

        Assert.Empty(db.JournalSlots.Where(s => s.PlacementId == placementId));
    }

    [Fact]
    public async Task GenerateDailyJournalSlots_InactivePlacement_SkipsPlacement()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, _) = await SeedActivePlacementAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), scope);
        var placement = await db.Placements.FindAsync(placementId);
        placement!.Status = PlacementStatus.Completed;
        await db.SaveChangesAsync();

        await Jobs(db).GenerateDailyJournalSlots(Monday);

        Assert.Empty(db.JournalSlots.Where(s => s.PlacementId == placementId));
    }

    private static DateOnly TodayJakarta() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, JournalCronJobs.JakartaTimeZone));

    [Fact]
    public async Task RemindEmptyJournals_EmptySlotWithLinkedStudent_CreatesNotificationAndOutboxEvent()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, FullName = "Siswa Uji Ingatkan", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = TodayJakarta(), Status = JournalSlotStatus.Empty };
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.JournalSlots.Add(slot);
        await db.SaveChangesAsync();
        var outboxBefore = db.OutboxMessages.Count();

        await Jobs(db).RemindEmptyJournals();

        var notif = Assert.Single(db.Notifications.Where(n => n.UserId == userId));
        Assert.Equal("JournalReminder", notif.Type);
        Assert.Equal(outboxBefore + 1, db.OutboxMessages.Count());
    }

    [Fact]
    public async Task RemindEmptyJournals_StudentWithoutLinkedUser_SkipsNotification()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = null, FullName = "Siswa Tanpa Akun", MajorId = Guid.NewGuid(), Classroom = "XII RPL 2" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = TodayJakarta(), Status = JournalSlotStatus.Empty };
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.JournalSlots.Add(slot);
        await db.SaveChangesAsync();

        await Jobs(db).RemindEmptyJournals();

        // Student.UserId null -> tak ada penerima valid -> nol notifikasi sama sekali dari test ini
        // (satu-satunya siswa/slot yang ada di scope DbContext ini).
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task RemindEmptyJournals_FilledSlot_NoNotification()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = userId, FullName = "Siswa Sudah Isi", MajorId = Guid.NewGuid(), Classroom = "XII RPL 3" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = TodayJakarta(), Status = JournalSlotStatus.Filled };
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.JournalSlots.Add(slot);
        await db.SaveChangesAsync();

        await Jobs(db).RemindEmptyJournals();

        Assert.Empty(db.Notifications.Where(n => n.UserId == userId));
    }
}
