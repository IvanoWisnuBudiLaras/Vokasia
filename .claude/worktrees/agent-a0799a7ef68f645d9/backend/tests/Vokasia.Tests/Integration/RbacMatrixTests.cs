using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H5-E3 §1 RbacMatrixTests — sampel sistematis matrix PRD 2.3: ≥15 kombinasi role×resource
/// yang HARUS ditolak (403/404), lewat HTTP end-to-end SUNGGUHAN (login real code+PKCE, Postgres
/// Testcontainers real - BUKAN InMemory) terhadap `VokasiaIntegrationFactory`. NFR-SEC-03.
///
/// Setiap test memetakan SATU baris matrix (role, endpoint, policy yang seharusnya menolak) - nama
/// method deskriptif dipakai sbg dokumentasi baris itu sendiri (pola sama AssessmentEndpointsTests
/// dst.), bukan Theory tunggal generik supaya kegagalan tiap baris tetap jelas siapa yang gagal.
/// </summary>
[Collection("IntegrationTests")]
public class RbacMatrixTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public RbacMatrixTests(VokasiaIntegrationFactory factory) => _factory = factory;

    // --- DeptHeadPlus (TenantAdmin/DeptHead) - Student/Teacher/Mentor HARUS ditolak ---

    [Fact]
    public async Task Student_CreatePeriod_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.Student, Guid.NewGuid(), "rbac-student-period");
        var resp = await client.PostAsJsonAsync("/api/periods", new { Name = "X", StartDate = "2026-01-01", EndDate = "2026-12-31", ClassLevels = "XII" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Teacher_CreateStudent_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.Teacher, Guid.NewGuid(), "rbac-teacher-student");
        var resp = await client.PostAsJsonAsync("/api/students", new { FullName = "X", MajorId = Guid.NewGuid(), Classroom = "XII A" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Mentor_CreatePlacement_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentor-placement");
        var resp = await client.PostAsJsonAsync("/api/placements", new { StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Student_SetHolidayCalendar_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.Student, Guid.NewGuid(), "rbac-student-holiday");
        var resp = await client.PutAsJsonAsync($"/api/periods/{Guid.NewGuid()}/holidays", new { Dates = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- TenantAdminOnly - DeptHead (bukan Admin!) HARUS ditolak juga, bukan cuma role rendah ---

    [Fact]
    public async Task DeptHead_CreateRubric_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.DeptHead, Guid.NewGuid(), "rbac-depthead-rubric");
        var resp = await client.PostAsJsonAsync("/api/rubrics", new { Name = "X", Aspects = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Teacher_FinalizeAssessment_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.Teacher, Guid.NewGuid(), "rbac-teacher-finalize");
        var resp = await client.PostAsJsonAsync($"/api/periods/{Guid.NewGuid()}/assessments/finalize", new { PlacementId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeptHead_InviteSchoolUser_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.DeptHead, Guid.NewGuid(), "rbac-depthead-invite");
        var resp = await client.PostAsJsonAsync("/api/school-users", new { Email = "x@y.test", Role = "Teacher", FullName = "X" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Mentor_ProposeCompany_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentor-company");
        var resp = await client.PostAsJsonAsync("/api/companies/propose", new { Name = "PT X" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- TeacherPlus - Student/Mentor HARUS ditolak ---

    [Fact]
    public async Task Student_CreateVisit_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.Student, Guid.NewGuid(), "rbac-student-visit");
        var resp = await client.PostAsJsonAsync($"/api/placements/{Guid.NewGuid()}/visits", new { Date = "2026-01-01", Notes = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Mentor_ListCompetencies_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentor-competencies");
        var resp = await client.GetAsync("/api/competencies");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- StudentSelf - role lain HARUS ditolak dari jurnal siswa ---

    [Fact]
    public async Task Teacher_GetTodayJournal_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.Teacher, Guid.NewGuid(), "rbac-teacher-journal");
        var resp = await client.GetAsync("/api/journals/today");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Mentor_SubmitJournal_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentor-submit");
        var resp = await client.PostAsync($"/api/journals/{Guid.NewGuid()}/submit", null);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- TenantMember - Mentor & SuperAdmin TIDAK PUNYA klaim tenant_id (lihat doc-comment RbacPolicies) ---

    [Fact]
    public async Task Mentor_ListPeriods_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentor-periods");
        var resp = await client.GetAsync("/api/periods?pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SuperAdmin_WithoutActingTenant_ListPeriods_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "rbac-sa-periods");
        var resp = await client.GetAsync("/api/periods?pageSize=10");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Mentor_GetGradeRecap_Rejected()
    {
        var (_, client) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentor-recap");
        var resp = await client.GetAsync($"/api/periods/{Guid.NewGuid()}/grade-recap");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- MentorOwnPlacement (resource-based) - Mentor B HARUS ditolak akses placement Mentor A ---

    [Fact]
    public async Task Mentor_SubmitScoresOnOtherMentorsPlacement_Rejected()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (mentorA, _) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentorA-scope");
        var (_, mentorBClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentorB-scope");

        var placementId = await SeedPlacementForMentorAsync(tenant.Id, mentorA.Id);

        var resp = await mentorBClient.PostAsJsonAsync($"/api/placements/{placementId}/assessment/mentor-scores", new object[]
        {
            new { AspectId = Guid.NewGuid(), Value = 80m },
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Mentor_GetAssessmentOnOtherMentorsPlacement_Rejected()
    {
        var tenant = await _factory.SeedTenantAsync();
        var (mentorA, _) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentorA-get");
        var (_, mentorBClient) = await _factory.LoginAsAsync(UserRole.IndustryMentor, null, "rbac-mentorB-get");

        var placementId = await SeedPlacementForMentorAsync(tenant.Id, mentorA.Id);

        var resp = await mentorBClient.GetAsync($"/api/placements/{placementId}/assessment");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // --- Cross-tenant TenantMember GET - HARUS 404 (placement tenant lain, bukan 403 - lihat doc-comment GetAssessment/GetPlacement) ---

    [Fact]
    public async Task TenantAdmin_GetPlacementFromOtherTenant_NotFound()
    {
        var otherTenant = await _factory.SeedTenantAsync("SMK Tenant Lain");
        var (otherAdmin, _) = await _factory.LoginAsAsync(UserRole.TenantAdmin, otherTenant.Id, "rbac-other-admin");
        var placementId = await SeedPlacementForMentorAsync(otherTenant.Id, Guid.NewGuid());

        var (_, myAdminClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, Guid.NewGuid(), "rbac-my-admin");
        var resp = await myAdminClient.GetAsync($"/api/placements/{placementId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    /// <summary>Placement minimal (student+company+period dummy) di-assign ke mentorUserId tertentu — dipakai suite resource-based (MentorOwnPlacement) &amp; isolasi tenant.</summary>
    private async Task<Guid> SeedPlacementForMentorAsync(Guid tenantId, Guid mentorUserId)
    {
        using var scope = _factory.CreateDbScope();
        var db = scope.ServiceProvider.GetRequiredService<Vokasia.Infrastructure.Persistence.VokasiaDbContext>();

        var period = new Vokasia.Domain.Entities.Period
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode RBAC",
            StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31),
            ClassLevels = "XII", Status = PeriodStatus.Active,
        };
        var company = new Vokasia.Domain.Entities.Company { Id = Guid.NewGuid(), Name = "PT RBAC" };
        var student = new Vokasia.Domain.Entities.Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa RBAC", MajorId = Guid.NewGuid(), Classroom = "XII A" };
        var placement = new Vokasia.Domain.Entities.Placement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id,
            PeriodId = period.Id, TeacherId = Guid.NewGuid(), MentorUserId = mentorUserId,
            Status = PlacementStatus.Active,
        };

        db.Periods.Add(period);
        db.Companies.Add(company);
        db.Students.Add(student);
        db.Placements.Add(placement);
        await db.SaveChangesAsync();
        return placement.Id;
    }
}
