using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Messaging;
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

    // Notifier sungguhan (bukan mock/fake) — sama seperti DI container produksi akan me-resolve
    // INotifier, dan test RemindEmptyJournals_* di bawah sudah menegaskan efek konkretnya lewat
    // db.Notifications langsung (Notifier hanya nulis baris Notification, tanpa side effect lain).
    private static JournalCronJobs Jobs(VokasiaDbContext db) => new(db, new Notifier(db), NullLogger<JournalCronJobs>.Instance);

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
        // Hitung DELTA (bukan Assert.Empty tabel utuh) - VokasiaApiFactory berbagi SATU database
        // InMemory lintas SEMUA test di kelas ini (IClassFixture), jadi test lain yg jalan
        // sebelum/sesudahnya bisa saja sudah menulis Notification-nya sendiri.
        var notifBefore = db.Notifications.Count();

        await Jobs(db).RemindEmptyJournals();

        // Student.UserId null -> tak ada penerima valid -> nol notifikasi BARU dari test ini.
        Assert.Equal(notifBefore, db.Notifications.Count());
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

    /// <summary>Seed 1 placement aktif + N JournalSlot mundur dari HARI INI (index 0=hari ini,
    /// 1=kemarin, dst - TIDAK perlu persis hari kerja kalender sungguhan, krn FlagGhostingStudents
    /// SENGAJA tak memfilter weekend lagi di sini, lihat doc-comment method itu: "setiap baris
    /// JournalSlot SUDAH PASTI hari kerja" krn diasumsikan ditulis GenerateDailyJournalSlots -
    /// utk uji LOGIKA hitung mundur murni ini cukup, hari-kerja-nyata sudah diuji terpisah).</summary>
    private async Task<(VokasiaDbContext Db, Guid PlacementId, Guid TenantId, Guid StudentId, Guid TeacherId, Guid PeriodId)> SeedPlacementWithRecentSlotsAsync(
        IServiceScope scope, params JournalSlotStatus[] statusesNewestFirst)
    {
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var today = TodayJakarta();
        var tenantId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var period = new Period
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Uji Ghosting",
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31),
            ClassLevels = "XII", Status = PeriodStatus.Active,
        };
        var placement = new Placement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentId, CompanyId = Guid.NewGuid(),
            PeriodId = period.Id, TeacherId = teacherId, Status = PlacementStatus.Active,
        };
        db.Periods.Add(period);
        db.Placements.Add(placement);
        db.Students.Add(new Student { Id = studentId, TenantId = tenantId, FullName = "Siswa Uji Ghosting", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" });

        for (var i = 0; i < statusesNewestFirst.Length; i++)
        {
            db.JournalSlots.Add(new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = today.AddDays(-i), Status = statusesNewestFirst[i] });
        }

        await db.SaveChangesAsync();
        return (db, placement.Id, tenantId, studentId, teacherId, period.Id);
    }

    [Fact]
    public async Task FlagGhostingStudents_NoEmptySlots_RagGreen_NoFlagNoNotification()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, _, studentId, teacherId, periodId) = await SeedPlacementWithRecentSlotsAsync(scope, JournalSlotStatus.Filled);

        await Jobs(db).FlagGhostingStudents();

        var status = await db.StudentDailyStatuses.SingleAsync(s => s.StudentId == studentId && s.PeriodId == periodId);
        Assert.Equal(RagStatus.Green, status.Rag);
        Assert.Empty(db.Notifications.Where(n => n.UserId == teacherId));
        // Dicek lewat isi payload (placementId TEST INI), BUKAN Count() tabel utuh - FlagGhostingStudents
        // SENGAJA lintas-tenant (lihat doc-comment JournalCronJobs), jadi VokasiaApiFactory yang
        // berbagi SATU database InMemory (IClassFixture) akan membuat pemanggilan job ini
        // MEMPROSES ULANG jg placement test LAIN yg sudah pernah di-seed sebelumnya di kelas ini -
        // Count() Type-only tak stabil lintas urutan test, isi payload placementId spesifik stabil.
        Assert.DoesNotContain(db.OutboxMessages, o => o.Type == "GhostingAlertEmailRequested" && o.PayloadJson.Contains(placementId.ToString()));
    }

    [Fact]
    public async Task FlagGhostingStudents_NoSlotsAtAll_RagGreen_StatusRowStillCreated()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, _, _, studentId, teacherId, periodId) = await SeedPlacementWithRecentSlotsAsync(scope); // nol slot sama sekali.

        await Jobs(db).FlagGhostingStudents();

        var status = await db.StudentDailyStatuses.SingleAsync(s => s.StudentId == studentId && s.PeriodId == periodId);
        Assert.Equal(RagStatus.Green, status.Rag);
        Assert.Empty(db.Notifications.Where(n => n.UserId == teacherId));
    }

    [Fact]
    public async Task FlagGhostingStudents_OneOrTwoConsecutiveEmpty_RagAmber_NoFlagNoNotification()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, _, studentId, teacherId, periodId) = await SeedPlacementWithRecentSlotsAsync(
            scope, JournalSlotStatus.Empty, JournalSlotStatus.Empty, JournalSlotStatus.Filled);

        await Jobs(db).FlagGhostingStudents();

        var status = await db.StudentDailyStatuses.SingleAsync(s => s.StudentId == studentId && s.PeriodId == periodId);
        Assert.Equal(RagStatus.Amber, status.Rag);
        // Amber (< 3 hari) TIDAK memicu notifikasi/outbox - ambang flag persis di >=3 (AC). Dicek
        // lewat isi payload (placementId test ini), bukan Count() tabel utuh - lihat catatan test Green.
        Assert.Empty(db.Notifications.Where(n => n.UserId == teacherId));
        Assert.DoesNotContain(db.OutboxMessages, o => o.Type == "GhostingAlertEmailRequested" && o.PayloadJson.Contains(placementId.ToString()));
    }

    [Fact]
    public async Task FlagGhostingStudents_ThreeOrMoreConsecutiveEmpty_RagRed_NotifiesTeacherAndTenantAdmin_WritesOutbox()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, tenantId, studentId, teacherId, periodId) = await SeedPlacementWithRecentSlotsAsync(
            scope, JournalSlotStatus.Empty, JournalSlotStatus.Empty, JournalSlotStatus.Empty, JournalSlotStatus.Filled);

        var tenantAdminId = Guid.NewGuid();
        var otherTenantAdminId = Guid.NewGuid(); // tenant BEDA - HARUS tak ikut dinotifikasi (isolasi tenant).
        db.Users.Add(new AppUser { Id = tenantAdminId, UserName = $"admin-{tenantAdminId:N}@vokasia.test", Email = $"admin-{tenantAdminId:N}@vokasia.test", FullName = "Admin Sekolah Uji", Role = UserRole.TenantAdmin, TenantId = tenantId, IsActive = true });
        db.Users.Add(new AppUser { Id = otherTenantAdminId, UserName = $"admin-{otherTenantAdminId:N}@vokasia.test", Email = $"admin-{otherTenantAdminId:N}@vokasia.test", FullName = "Admin Sekolah Lain", Role = UserRole.TenantAdmin, TenantId = Guid.NewGuid(), IsActive = true });
        await db.SaveChangesAsync();

        await Jobs(db).FlagGhostingStudents();

        var status = await db.StudentDailyStatuses.SingleAsync(s => s.StudentId == studentId && s.PeriodId == periodId);
        Assert.Equal(RagStatus.Red, status.Rag);
        Assert.Single(db.Notifications.Where(n => n.UserId == teacherId && n.Type == "GhostingAlert"));
        Assert.Single(db.Notifications.Where(n => n.UserId == tenantAdminId && n.Type == "GhostingAlert"));
        Assert.Empty(db.Notifications.Where(n => n.UserId == otherTenantAdminId));
        // Dicek lewat isi payload (placementId TEST INI), bukan Count()/Single() tabel utuh -
        // FlagGhostingStudents lintas-tenant by design (lihat doc-comment JournalCronJobs), jadi
        // VokasiaApiFactory yg berbagi SATU database InMemory (IClassFixture) akan membuat
        // pemanggilan job ini MEMPROSES ULANG jg placement test LAIN yg sudah >=3 hari kosong dari
        // test sebelumnya di kelas ini - Count()/Single() tabel utuh tak stabil lintas urutan test.
        Assert.Single(db.OutboxMessages.Where(o => o.Type == "GhostingAlertEmailRequested" && o.PayloadJson.Contains(placementId.ToString())));
    }

    [Fact]
    public async Task FlagGhostingStudents_InactivePlacement_SkippedEntirely()
    {
        using var scope = _factory.Services.CreateScope();
        var (db, placementId, _, studentId, _, periodId) = await SeedPlacementWithRecentSlotsAsync(
            scope, JournalSlotStatus.Empty, JournalSlotStatus.Empty, JournalSlotStatus.Empty, JournalSlotStatus.Empty);
        var placement = await db.Placements.FindAsync(placementId);
        placement!.Status = PlacementStatus.Completed;
        await db.SaveChangesAsync();

        await Jobs(db).FlagGhostingStudents();

        Assert.Empty(db.StudentDailyStatuses.Where(s => s.StudentId == studentId && s.PeriodId == periodId));
    }
}
