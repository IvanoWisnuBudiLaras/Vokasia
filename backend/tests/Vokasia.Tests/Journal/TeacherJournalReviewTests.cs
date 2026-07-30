using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Journal;

/// <summary>
/// AC VOK-H4-E2 (halaman guru bimbingan): (1) `ListPlacements?teacherId=` — filter BARU (gap
/// ditemukan: endpoint H2-E1 ini sebelumnya tak punya cara sama sekali utk "placement milik guru
/// X" — lihat komentar filter di CompaniesAndPlacements.cs); (2) `GET /api/journals/for-teacher/
/// {placementId}` — endpoint BARU (gap: `ListJournals` lama terkunci `StudentSelf` + look-up
/// internal by Student.UserId, TIDAK BISA dipakai guru sama sekali — lihat doc-comment
/// JournalWithCommentsDto). Fokus suite ini pada kebenaran filter/proyeksi BARU, bukan mengulang
/// RbacPolicyTests (TeacherPlus sudah dites di sana).
/// </summary>
public class TeacherJournalReviewTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public TeacherJournalReviewTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AuthenticatedTeacherClientAsync(Guid tenantId, string emailPrefix = "teacher-review")
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, emailPrefix, UserRole.Teacher, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (client);
    }

    private async Task<(HttpClient Client, Guid TeacherId)> AuthenticatedTeacherContextAsync(Guid tenantId, string emailPrefix)
    {
        var user = await AuthTestHelpers.SeedUserAsync(_factory, emailPrefix, UserRole.Teacher, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return (client, user.Id);
    }

    [Fact]
    public async Task ListPlacements_TeacherIdFilter_ReturnsOnlyThatTeachersAssignments()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId);
        var myTeacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();

        Guid periodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Bimbingan", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Bimbingan" };
            var myStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Bimbinganku", MajorId = Guid.NewGuid(), Classroom = "XII A" };
            var otherStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Guru Lain", MajorId = Guid.NewGuid(), Classroom = "XII B" };
            var myPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = myStudent.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = myTeacherId, Status = PlacementStatus.Active };
            var otherPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = otherStudent.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = otherTeacherId, Status = PlacementStatus.Active };

            db.Periods.Add(period);
            db.Companies.Add(company);
            db.Students.AddRange(myStudent, otherStudent);
            db.Placements.AddRange(myPlacement, otherPlacement);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/placements?periodId={periodId}&teacherId={myTeacherId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(myTeacherId, body.GetProperty("items")[0].GetProperty("teacherId").GetGuid());
    }

    [Fact]
    public async Task ListPlacements_WithoutTeacherIdFilter_BehavesExactlyAsBefore()
    {
        // AC: filter BARU bersifat opsional - pemanggil lama (tanpa teacherId) tak boleh terdampak.
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId, "teacher-nofilter");

        Guid periodId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Tanpa Filter", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Tanpa Filter" };
            var studentA = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa A", MajorId = Guid.NewGuid(), Classroom = "XII A" };
            var studentB = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa B", MajorId = Guid.NewGuid(), Classroom = "XII B" };
            db.Periods.Add(period);
            db.Companies.Add(company);
            db.Students.AddRange(studentA, studentB);
            db.Placements.AddRange(
                new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentA.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active },
                new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = studentB.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active });
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/placements?periodId={periodId}");

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32()); // keduanya tetap muncul.
    }

    [Fact]
    public async Task ListPlacements_StudentIdFilter_ReturnsOnlyThatStudentsPlacement()
    {
        // Dipakai StudentDetailDrawer (dashboard W3): dari DashboardFlaggedStudentDto cuma py
        // studentId, butuh cari placement-nya dulu sebelum panggil for-teacher/{placementId}.
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId, "teacher-studentfilter");

        Guid periodId, targetStudentId, targetPlacementId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Drawer", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            periodId = period.Id;
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Drawer" };
            var targetStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Target", MajorId = Guid.NewGuid(), Classroom = "XII A" };
            targetStudentId = targetStudent.Id;
            var otherStudent = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Lain", MajorId = Guid.NewGuid(), Classroom = "XII B" };
            var targetPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = targetStudent.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            targetPlacementId = targetPlacement.Id;
            var otherPlacement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = otherStudent.Id, CompanyId = company.Id, PeriodId = periodId, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };

            db.Periods.Add(period);
            db.Companies.Add(company);
            db.Students.AddRange(targetStudent, otherStudent);
            db.Placements.AddRange(targetPlacement, otherPlacement);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/placements?periodId={periodId}&studentId={targetStudentId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(targetPlacementId, body.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ListJournalsForTeacher_ReturnsEntriesNewestFirstWithChronologicalComments()
    {
        var tenantId = Guid.NewGuid();
        var (client, teacherId) = await AuthenticatedTeacherContextAsync(tenantId, "teacher-entries");

        Guid placementId;
        Guid olderEntryId, newerEntryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Review", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Review" };
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Direview", MajorId = Guid.NewGuid(), Classroom = "XII A" };
            var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = teacherId, Status = PlacementStatus.Active };
            placementId = placement.Id;

            var olderSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = new DateOnly(2026, 7, 1), Status = JournalSlotStatus.Filled };
            var olderEntry = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SlotId = olderSlot.Id, PlacementId = placement.Id,
                Text = "Entri lebih lama", Status = JournalEntryStatus.Approved,
                SubmittedAt = new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero),
            };
            olderEntryId = olderEntry.Id;

            var newerSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = new DateOnly(2026, 7, 2), Status = JournalSlotStatus.Filled };
            var newerEntry = new JournalEntry
            {
                Id = Guid.NewGuid(), TenantId = tenantId, SlotId = newerSlot.Id, PlacementId = placement.Id,
                Text = "Entri lebih baru", Status = JournalEntryStatus.Submitted,
                SubmittedAt = new DateTimeOffset(2026, 7, 2, 8, 0, 0, TimeSpan.Zero),
            };
            newerEntryId = newerEntry.Id;

            var commentEarlier = new TeacherComment
            {
                Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryId = olderEntry.Id, TeacherId = Guid.NewGuid(),
                Text = "Komentar duluan", CreatedAt = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.Zero),
            };
            var commentLater = new TeacherComment
            {
                Id = Guid.NewGuid(), TenantId = tenantId, JournalEntryId = olderEntry.Id, TeacherId = Guid.NewGuid(),
                Text = "Komentar belakangan", CreatedAt = new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero),
            };

            db.Periods.Add(period);
            db.Companies.Add(company);
            db.Students.Add(student);
            db.Placements.Add(placement);
            db.JournalSlots.AddRange(olderSlot, newerSlot);
            db.JournalEntries.AddRange(olderEntry, newerEntry);
            // Sengaja Add dlm urutan "belakangan dulu" - membuktikan sorting hasil query, BUKAN
            // kebetulan cocok urutan insert.
            db.TeacherComments.AddRange(commentLater, commentEarlier);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync($"/api/journals/for-teacher/{placementId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetArrayLength());

        // Entri terbaru (SubmittedAt lebih besar) tampil DULU.
        Assert.Equal(newerEntryId, body[0].GetProperty("entry").GetProperty("id").GetGuid());
        Assert.Equal(olderEntryId, body[1].GetProperty("entry").GetProperty("id").GetGuid());
        Assert.Equal(0, body[0].GetProperty("comments").GetArrayLength());

        var commentsOnOlder = body[1].GetProperty("comments");
        Assert.Equal(2, commentsOnOlder.GetArrayLength());
        Assert.Equal("Komentar duluan", commentsOnOlder[0].GetProperty("text").GetString());
        Assert.Equal("Komentar belakangan", commentsOnOlder[1].GetProperty("text").GetString());
    }

    [Fact]
    public async Task ListJournalsForTeacher_PlacementDoesNotExist_Returns404()
    {
        var tenantId = Guid.NewGuid();
        var client = await AuthenticatedTeacherClientAsync(tenantId, "teacher-404");

        var resp = await client.GetAsync($"/api/journals/for-teacher/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task ListJournalsForTeacher_UnassignedPlacement_Returns403()
    {
        var tenantId = Guid.NewGuid();
        var teacher = await AuthTestHelpers.SeedUserAsync(_factory, "teacher-unassigned-list", UserRole.Teacher, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, teacher.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        Guid placementId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Unassigned", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var company = new Company { Id = Guid.NewGuid(), Name = "PT Unassigned" };
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Unassigned", MajorId = Guid.NewGuid(), Classroom = "XII" };
            var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = company.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            placementId = placement.Id;
            db.Periods.Add(period);
            db.Companies.Add(company);
            db.Students.Add(student);
            db.Placements.Add(placement);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync($"/api/journals/for-teacher/{placementId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
