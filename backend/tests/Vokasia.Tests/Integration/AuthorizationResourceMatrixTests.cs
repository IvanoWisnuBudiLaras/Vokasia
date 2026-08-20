extern alias ApiAssembly;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Identity;
using Vokasia.Infrastructure.Persistence;
using Xunit;

namespace Vokasia.Tests.Integration;

/// <summary>HTTP/resource authorization matrix. These are intentionally not policy-only tests.</summary>
public sealed class AuthorizationResourceMatrixTests : IClassFixture<VokasiaIntegrationFactory>
{
    private readonly VokasiaIntegrationFactory _factory;
    public AuthorizationResourceMatrixTests(VokasiaIntegrationFactory factory) => _factory = factory;

    private sealed record Fx(Guid TenantA, Guid TenantB, Guid PeriodA, Guid PlacementA1, Guid PlacementA2, Guid PlacementB, Guid InvoiceB, Guid StudentA1, Guid StudentA2, Guid StudentB, Guid EntryA1, Guid EntryA2, AppUser AdminA, AppUser TeacherA1, AppUser TeacherA2, AppUser MentorA1, AppUser MentorA2, AppUser StudentA1User, HttpClient AdminClient, HttpClient TeacherA1Client, HttpClient TeacherA2Client, HttpClient MentorA1Client, HttpClient MentorA2Client, HttpClient StudentA1Client);

    private async Task<Fx> SeedAsync()
    {
        var tenantA = await _factory.SeedTenantAsync($"Tenant A {Guid.NewGuid():N}");
        var tenantB = await _factory.SeedTenantAsync($"Tenant B {Guid.NewGuid():N}");
        var admin = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenantA.Id, $"matrix-admin-{Guid.NewGuid():N}");
        var teacher1 = await _factory.LoginAsAsync(UserRole.Teacher, tenantA.Id, $"matrix-teacher1-{Guid.NewGuid():N}");
        var teacher2 = await _factory.LoginAsAsync(UserRole.Teacher, tenantA.Id, $"matrix-teacher2-{Guid.NewGuid():N}");
        var mentor1 = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, $"matrix-mentor1-{Guid.NewGuid():N}");
        var mentor2 = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, $"matrix-mentor2-{Guid.NewGuid():N}");
        var student1 = await _factory.LoginAsAsync(UserRole.Student, tenantA.Id, $"matrix-student1-{Guid.NewGuid():N}");
        var student2 = await _factory.LoginAsAsync(UserRole.Student, tenantA.Id, $"matrix-student2-{Guid.NewGuid():N}");
        var studentB = await _factory.LoginAsAsync(UserRole.Student, tenantB.Id, $"matrix-studentb-{Guid.NewGuid():N}");

        var periodA = Guid.NewGuid();
        var periodB = Guid.NewGuid();
        var placementA1 = Guid.NewGuid();
        var placementA2 = Guid.NewGuid();
        var placementB = Guid.NewGuid();
        var invoiceB = Guid.NewGuid();
        var entryA1 = Guid.NewGuid();
        var entryA2 = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var companyA = new Company { Id = Guid.NewGuid(), Name = "DUDI Matrix A" };
            var companyB = new Company { Id = Guid.NewGuid(), Name = "DUDI Matrix B" };
            var period = new Period { Id = periodA, TenantId = tenantA.Id, Name = "Periode Matrix", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var tenantBPeriod = new Period { Id = periodB, TenantId = tenantB.Id, Name = "Periode Matrix B", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var sa1 = new Student { Id = Guid.NewGuid(), TenantId = tenantA.Id, UserId = student1.User.Id, FullName = "Student A1", Nisn = "MATRIX-A1", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
            var sa2 = new Student { Id = Guid.NewGuid(), TenantId = tenantA.Id, UserId = student2.User.Id, FullName = "Student A2", Nisn = "MATRIX-A2", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
            var sb = new Student { Id = Guid.NewGuid(), TenantId = tenantB.Id, UserId = studentB.User.Id, FullName = "Student B1", Nisn = "MATRIX-B1", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
            db.Companies.AddRange(companyA, companyB); db.Periods.AddRange(period, tenantBPeriod); db.Students.AddRange(sa1, sa2, sb);
            db.Invoices.Add(new Invoice { Id = invoiceB, TenantId = tenantB.Id, Amount = 1000m, PeriodMonth = new DateOnly(2026, 1, 1), Status = InvoiceStatus.Issued });
            var slotA1 = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantA.Id, PlacementId = placementA1, Date = new DateOnly(2026, 8, 18), Status = JournalSlotStatus.Filled };
            var slotA2 = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantA.Id, PlacementId = placementA2, Date = new DateOnly(2026, 8, 18), Status = JournalSlotStatus.Filled };
            db.JournalSlots.AddRange(slotA1, slotA2);
            db.JournalEntries.AddRange(
                new JournalEntry { Id = entryA1, TenantId = tenantA.Id, SlotId = slotA1.Id, PlacementId = placementA1, Text = "Matrix journal A1", Status = JournalEntryStatus.Submitted },
                new JournalEntry { Id = entryA2, TenantId = tenantA.Id, SlotId = slotA2.Id, PlacementId = placementA2, Text = "Matrix journal A2", Status = JournalEntryStatus.Submitted });
            db.Placements.AddRange(
                new Placement { Id = placementA1, TenantId = tenantA.Id, StudentId = sa1.Id, CompanyId = companyA.Id, PeriodId = periodA, TeacherId = teacher1.User.Id, MentorUserId = mentor1.User.Id },
                new Placement { Id = placementA2, TenantId = tenantA.Id, StudentId = sa2.Id, CompanyId = companyA.Id, PeriodId = periodA, TeacherId = teacher2.User.Id, MentorUserId = mentor2.User.Id },
                new Placement { Id = placementB, TenantId = tenantB.Id, StudentId = sb.Id, CompanyId = companyB.Id, PeriodId = periodB, TeacherId = teacher1.User.Id, MentorUserId = mentor1.User.Id });
            await db.SaveChangesAsync();
            return new Fx(tenantA.Id, tenantB.Id, periodA, placementA1, placementA2, placementB, invoiceB, sa1.Id, sa2.Id, sb.Id, entryA1, entryA2, admin.User, teacher1.User, teacher2.User, mentor1.User, mentor2.User, student1.User, admin.Client, teacher1.Client, teacher2.Client, mentor1.Client, mentor2.Client, student1.Client);
        }
    }

    [Fact] public async Task TenantAdmin_SameTenant_AssignTeacher_Allowed() { var fx = await SeedAsync(); var target = await _factory.LoginAsAsync(UserRole.Teacher, fx.TenantA, $"target-teacher-{Guid.NewGuid():N}"); var r = await fx.AdminClient.PutAsJsonAsync($"/api/school-users/{target.User.Id}/role", UserRole.Teacher); Assert.Equal(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task TenantAdmin_SameTenant_AssignDeptHead_Allowed() { var fx = await SeedAsync(); var target = await _factory.LoginAsAsync(UserRole.Teacher, fx.TenantA, $"target-{Guid.NewGuid():N}"); var r = await fx.AdminClient.PutAsJsonAsync($"/api/school-users/{target.User.Id}/role", UserRole.DeptHead); Assert.Equal(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task TenantAdmin_AssignSuperAdmin_Denied() { var fx = await SeedAsync(); var sa = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, $"sa-{Guid.NewGuid():N}"); var r = await fx.AdminClient.PutAsJsonAsync($"/api/school-users/{sa.User.Id}/role", UserRole.SuperAdmin); Assert.NotEqual(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task TenantAdmin_TenantA_AssignUserTenantB_Denied() { var fx = await SeedAsync(); var b = await _factory.LoginAsAsync(UserRole.Teacher, fx.TenantB, $"b-{Guid.NewGuid():N}"); var r = await fx.AdminClient.PutAsJsonAsync($"/api/school-users/{b.User.Id}/role", UserRole.Teacher); Assert.True(r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden); }
    [Fact] public async Task Teacher_AssignRole_Denied() { var fx = await SeedAsync(); var r = await fx.TeacherA1Client.PutAsJsonAsync($"/api/school-users/{fx.TeacherA2.Id}/role", UserRole.Teacher); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Student_AssignRole_Denied() { var fx = await SeedAsync(); var r = await fx.StudentA1Client.PutAsJsonAsync($"/api/school-users/{fx.TeacherA1.Id}/role", UserRole.Teacher); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }

    [Fact] public async Task StudentA_ReadStudentB_Denied() { var fx = await SeedAsync(); var r = await fx.StudentA1Client.GetAsync("/api/students"); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Student_ReadSchoolRoster_Denied() => await StudentA_ReadStudentB_Denied();
    [Fact] public async Task Student_ReadTenantGradeRecap_Denied() { var fx = await SeedAsync(); var r = await fx.StudentA1Client.GetAsync($"/api/periods/{fx.PeriodA}/grade-recap"); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task StudentA_ReadStudentBAssessment_Denied() { var fx = await SeedAsync(); var r = await fx.StudentA1Client.GetAsync($"/api/placements/{fx.PlacementA2}/assessment"); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task StudentA_ReadStudentBPlacement_Denied() { var fx = await SeedAsync(); var r = await fx.StudentA1Client.GetAsync($"/api/placements/{fx.PlacementA2}"); Assert.NotEqual(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task StudentA_ReadStudentBPrivatePortfolio_Denied() { var fx = await SeedAsync(); var r = await fx.StudentA1Client.GetAsync($"/api/portfolio/student/{fx.StudentA2}"); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Student_ReadSchoolDashboard_Denied() { var fx = await SeedAsync(); var r = await fx.StudentA1Client.GetAsync($"/api/dashboard/school/{fx.PeriodA}"); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }

    [Fact] public async Task Teacher_ReadAssignedPlacement_Allowed() { var fx = await SeedAsync(); var r = await fx.TeacherA1Client.GetAsync($"/api/placements/{fx.PlacementA1}"); Assert.Equal(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task Teacher_ReadOtherTeacherPlacement_Denied() { var fx = await SeedAsync(); var r = await fx.TeacherA1Client.GetAsync($"/api/placements/{fx.PlacementA2}"); Assert.NotEqual(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task Teacher_WriteAssessmentForAssignedPlacement_Allowed() { var fx = await SeedAsync(); var r = await fx.TeacherA1Client.PostAsJsonAsync($"/api/placements/{fx.PlacementA1}/assessment/teacher-scores", Array.Empty<object>()); Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Teacher_WriteAssessmentForOtherTeacherPlacement_Denied() { var fx = await SeedAsync(); var r = await fx.TeacherA1Client.PostAsJsonAsync($"/api/placements/{fx.PlacementA2}/assessment/teacher-scores", Array.Empty<object>()); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Teacher_PrivilegedJournalActionForUnauthorizedPlacement_Denied() { var fx = await SeedAsync(); var r = await fx.TeacherA1Client.GetAsync($"/api/journals/for-teacher/{fx.PlacementA2}"); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }

    [Fact] public async Task Mentor_ReadOwnPlacement_Allowed() { var fx = await SeedAsync(); var r = await fx.MentorA1Client.GetAsync($"/api/placements/{fx.PlacementA1}"); Assert.Equal(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task Mentor_ReadOtherMentorPlacement_Denied() { var fx = await SeedAsync(); var r = await fx.MentorA1Client.GetAsync($"/api/placements/{fx.PlacementA2}"); Assert.NotEqual(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task Mentor_WriteOwnAssessment_Allowed() { var fx = await SeedAsync(); var r = await fx.MentorA1Client.PostAsJsonAsync($"/api/placements/{fx.PlacementA1}/assessment/mentor-scores", Array.Empty<object>()); Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Mentor_WriteOtherMentorAssessment_Denied() { var fx = await SeedAsync(); var r = await fx.MentorA1Client.PostAsJsonAsync($"/api/placements/{fx.PlacementA2}/assessment/mentor-scores", Array.Empty<object>()); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Mentor_ApproveOwnPlacementJournal_Allowed() { var fx = await SeedAsync(); var r = await fx.MentorA1Client.PostAsJsonAsync($"/api/journals/{fx.EntryA1}/approve", new { Note = "ok" }); Assert.NotEqual(HttpStatusCode.Forbidden, r.StatusCode); }
    [Fact] public async Task Mentor_ApproveOtherMentorJournal_Denied() { var fx = await SeedAsync(); var r = await fx.MentorA1Client.PostAsJsonAsync($"/api/journals/{fx.EntryA2}/approve", new { Note = "ok" }); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); }

    [Fact] public async Task TenantAStaff_ReadTenantBStudent_Denied() { var fx = await SeedAsync(); var r = await fx.AdminClient.GetAsync("/api/students"); Assert.Equal(HttpStatusCode.OK, r.StatusCode); var body = await r.Content.ReadFromJsonAsync<JsonElement>(); Assert.DoesNotContain("Student B1", body.ToString(), StringComparison.Ordinal); Assert.DoesNotContain("MATRIX-B1", body.ToString(), StringComparison.Ordinal); }
    [Fact] public async Task TenantAStaff_ReadTenantBPlacement_Denied() { var fx = await SeedAsync(); var r = await fx.AdminClient.GetAsync($"/api/placements/{fx.PlacementB}"); Assert.NotEqual(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task TenantAStaff_ReadTenantBAssessment_Denied() { var fx = await SeedAsync(); var r = await fx.AdminClient.GetAsync($"/api/placements/{fx.PlacementB}/assessment"); Assert.NotEqual(HttpStatusCode.OK, r.StatusCode); }
    [Fact] public async Task TenantAStaff_ReadTenantBInvoice_Denied() { var fx = await SeedAsync(); var r = await fx.AdminClient.PostAsJsonAsync($"/api/invoices/{fx.InvoiceB}/payment-proof", new { ObjectKey = $"tenant/{fx.TenantB}/invoices/{fx.InvoiceB}/proof.pdf" }); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); }
}
