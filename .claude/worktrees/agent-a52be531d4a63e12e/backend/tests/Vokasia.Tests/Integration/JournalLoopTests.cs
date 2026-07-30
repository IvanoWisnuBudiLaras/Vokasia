using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 JournalLoopTests — submit (HTTP) → OutboxMessage ditulis → OutboxDispatcher
/// (Worker sungguhan, poll 2 dtk) → publish RabbitMQ Testcontainers → JournalSubmittedConsumer
/// (StudentDailyStatus.Rag→Green) + StreakCounterConsumer (Streak→1) → mentor approve (HTTP) →
/// JournalApprovedConsumer (notifikasi siswa). Verifikasi async LEWAT PollUntil (bukan sleep buta -
/// OutboxDispatcher polling interval 2 dtk BUKAN instan, lihat MessagingDefaults/OutboxDispatcher).
///
/// [GAP dicatat, BUKAN kelalaian suite ini] "+ proyeksi entri ke bahan portofolio" (kalimat ticket)
/// TIDAK diuji di sini - dikonfirmasi baca kode JournalApprovedConsumer sendiri: field/tabel
/// "kompetensi terverifikasi" TIDAK ADA di skema manapun sampai sesi ini, gap sudah didokumentasikan
/// eksplisit di doc-comment consumer tsb (kemungkinan ticket H6 tersendiri) - menguji sesuatu yang
/// tidak diimplementasikan akan jadi test palsu/mengarang ekspektasi, bukan pembuktian jujur.
/// </summary>
[Collection("IntegrationTests")]
public class JournalLoopTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public JournalLoopTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private sealed record Fixture(Guid TenantId, Guid PeriodId, Guid PlacementId, Guid SlotId, DateOnly Date, Guid StudentId, Guid StudentUserId, Guid MentorUserId);

    private async Task<Fixture> SeedActiveSlotAsync(Guid tenantId, Guid studentUserId, Guid mentorUserId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Loop", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Loop" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = studentUserId, FullName = "Siswa Loop", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), MentorUserId = mentorUserId, Status = PlacementStatus.Active };
        var today = AppTimeZone.TodayJakarta();
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = today, Status = JournalSlotStatus.Empty };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.JournalSlots.Add(slot);
        await db.SaveChangesAsync();

        return new Fixture(tenantId, period.Id, placement.Id, slot.Id, today, student.Id, studentUserId, mentorUserId);
    }

    [Fact]
    public async Task SubmitJournal_ProjectsStudentDailyStatusGreenAndStreak_ViaRealOutboxAndConsumers()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (student, studentClient) = await _factory.LoginAsAsync(UserRole.Student, tenant.Id, "loop-student-submit");
        var fx = await SeedActiveSlotAsync(tenant.Id, student.Id, Guid.NewGuid());

        var submitResp = await studentClient.PostAsJsonAsync($"/api/journals/{fx.SlotId}/submit", new
        {
            SlotId = fx.SlotId,
            Text = "Belajar wiring listrik dasar hari ini.",
            CompetencyIds = Array.Empty<Guid>(),
            PhotoIds = (List<Guid>?)null,
        });
        submitResp.EnsureSuccessStatusCode();

        await PollUntil.SucceedsAsync(async () =>
        {
            using var scope = _factory.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var status = await db.StudentDailyStatuses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudentId == fx.StudentId && x.PeriodId == fx.PeriodId && x.Date == fx.Date);
            Assert.NotNull(status);
            Assert.Equal(RagStatus.Green, status!.Rag);
            Assert.True(status.Streak >= 1);
        }, timeout: TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task MentorApproveJournal_NotifiesStudent_ViaRealOutboxAndConsumer()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (student, studentClient) = await _factory.LoginAsAsync(UserRole.Student, tenant.Id, "loop-student-approve");
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "loop-mentor-approve");
        var fx = await SeedActiveSlotAsync(tenant.Id, student.Id, mentor.Id);

        var submitResp = await studentClient.PostAsJsonAsync($"/api/journals/{fx.SlotId}/submit", new
        {
            SlotId = fx.SlotId,
            Text = "Jurnal siap disetujui mentor.",
            CompetencyIds = Array.Empty<Guid>(),
            PhotoIds = (List<Guid>?)null,
        });
        submitResp.EnsureSuccessStatusCode();
        var submitBody = await submitResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var entryId = submitBody.GetProperty("id").GetGuid();

        var approveResp = await mentorClient.PostAsJsonAsync($"/api/journals/{entryId}/approve", new { Note = "Bagus, lanjutkan." });
        Assert.Equal(HttpStatusCode.OK, approveResp.StatusCode);

        await PollUntil.SucceedsAsync(async () =>
        {
            using var scope = _factory.CreateDbScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var notif = await db.Notifications.AsNoTracking()
                .FirstOrDefaultAsync(n => n.UserId == student.Id && n.Type == "JournalApproved");
            Assert.NotNull(notif);
        }, timeout: TimeSpan.FromSeconds(15));
    }
}
