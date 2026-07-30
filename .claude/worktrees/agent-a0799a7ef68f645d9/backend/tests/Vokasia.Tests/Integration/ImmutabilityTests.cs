using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 ImmutabilityTests — approve (jurnal) / finalize (assessment) → mutasi lanjutan
/// oleh peran mana pun → 409 {code,message} (DomainImmutableExceptionHandler, NFR-SEC-08), lewat
/// HTTP+Postgres sungguhan (bukan InMemory - suite unit Guard/ImmutabilityTests.cs sudah cover
/// unit murni; di sini membuktikan pipeline HTTP END-TO-END: middleware exception handler + EF
/// Core Postgres round-trip yang SAMA menghasilkan 409, bukan cuma method C# yang throw).
/// </summary>
[Collection("IntegrationTests")]
public class ImmutabilityTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public ImmutabilityTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private sealed record JournalFixture(Guid TenantId, Guid PlacementId, Guid EntryId, Guid MentorUserId);

    private async Task<JournalFixture> SeedApprovedJournalAsync(Guid tenantId, Guid studentUserId, Guid mentorUserId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Immut", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Immut" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = studentUserId, FullName = "Siswa Immut", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), MentorUserId = mentorUserId, Status = PlacementStatus.Active };
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
        var entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slot.Id, PlacementId = placement.Id, Text = "Jurnal sudah disetujui", Status = JournalEntryStatus.Approved, SubmittedAt = DateTimeOffset.UtcNow, ApprovedAt = DateTimeOffset.UtcNow };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.JournalSlots.Add(slot);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();

        return new JournalFixture(tenantId, placement.Id, entry.Id, mentorUserId);
    }

    private async Task<HttpResponseMessage> Conflict409BodyAssertAsync(HttpResponseMessage resp)
    {
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("journal-approved-immutable", body.GetProperty("code").GetString());
        return resp;
    }

    [Fact]
    public async Task Student_AttachPhotoOnApprovedEntry_Returns409()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (student, studentClient) = await _factory.LoginAsAsync(UserRole.Student, tenant.Id, "immut-student-photo");
        var fx = await SeedApprovedJournalAsync(tenant.Id, student.Id, Guid.NewGuid());

        var resp = await studentClient.PostAsJsonAsync($"/api/journals/{fx.EntryId}/photos", new { ObjectKey = "journal/x.jpg" });
        await Conflict409BodyAssertAsync(resp);
    }

    [Fact]
    public async Task Mentor_ApproveAlreadyApprovedEntry_Returns409()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "immut-mentor-approve");
        var fx = await SeedApprovedJournalAsync(tenant.Id, Guid.NewGuid(), mentor.Id);

        var resp = await mentorClient.PostAsJsonAsync($"/api/journals/{fx.EntryId}/approve", new { Note = (string?)null });
        await Conflict409BodyAssertAsync(resp);
    }

    [Fact]
    public async Task Mentor_RejectAlreadyApprovedEntry_Returns409()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "immut-mentor-reject");
        var fx = await SeedApprovedJournalAsync(tenant.Id, Guid.NewGuid(), mentor.Id);

        var resp = await mentorClient.PostAsJsonAsync($"/api/journals/{fx.EntryId}/reject", new { Reason = "Alasan penolakan uji coba" });
        await Conflict409BodyAssertAsync(resp);
    }

    // --- Assessment: finalize -> revisi skor (mentor & guru) -> 409 ---

    private sealed record AssessmentFixture(Guid TenantId, Guid PlacementId, Guid TeknisId, Guid SoftskillId, Guid KehadiranId);

    private async Task<AssessmentFixture> SeedFinalizedAssessmentAsync(Guid tenantId, Guid mentorUserId, Guid teacherUserId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var teknis = new RubricAspect { Id = Guid.NewGuid(), Name = "Teknis", Kind = RubricAspectKind.Teknis, Weight = 40 };
        var softskill = new RubricAspect { Id = Guid.NewGuid(), Name = "Softskill", Kind = RubricAspectKind.Softskill, Weight = 40 };
        var kehadiran = new RubricAspect { Id = Guid.NewGuid(), Name = "Kehadiran", Kind = RubricAspectKind.Kehadiran, Weight = 20 };
        var rubric = new RubricTemplate { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rubrik Immut", IsDefault = true, Aspects = [teknis, softskill, kehadiran] };
        foreach (var a in rubric.Aspects) a.RubricTemplateId = rubric.Id;

        var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Immut Assess", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Assessment };
        var company = new Company { Id = Guid.NewGuid(), Name = "PT Immut Assess" };
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Immut Assess", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = teacherUserId, MentorUserId = mentorUserId, Status = PlacementStatus.Active };
        var assessment = new Vokasia.Domain.Entities.Assessment
        {
            Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, RubricTemplateId = rubric.Id,
            IsFinal = true, FinalScore = 85m, FinalizedAt = DateTimeOffset.UtcNow,
        };

        db.RubricTemplates.Add(rubric);
        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync();

        return new AssessmentFixture(tenantId, placement.Id, teknis.Id, softskill.Id, kehadiran.Id);
    }

    [Fact]
    public async Task Mentor_ReviseScoresAfterFinalize_Returns409()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (mentor, mentorClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "immut-mentor-revise");
        var fx = await SeedFinalizedAssessmentAsync(tenant.Id, mentor.Id, Guid.NewGuid());

        var resp = await mentorClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = fx.TeknisId, Value = 99m },
        });
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("assessment-final-immutable", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Teacher_ReviseScoresAfterFinalize_Returns409()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (teacher, teacherClient) = await _factory.LoginAsAsync(UserRole.Teacher, tenant.Id, "immut-teacher-revise");
        var fx = await SeedFinalizedAssessmentAsync(tenant.Id, Guid.NewGuid(), teacher.Id);

        var resp = await teacherClient.PostAsJsonAsync($"/api/placements/{fx.PlacementId}/assessment/teacher-scores", new object[]
        {
            new { AspectId = fx.SoftskillId, Value = 99m },
        });
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        Assert.Equal("assessment-final-immutable", body.GetProperty("code").GetString());
    }
}
