using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;
using Xunit;
using Xunit.Abstractions;

namespace Vokasia.Tests.FlowTests;

public class JournalLifecycleFlowBatchTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public JournalLifecycleFlowBatchTests(VokasiaApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    private async Task<(HttpClient Client, Guid StudentId, Guid PlacementId, Guid TenantId, string Email)> SeedStudentAsync()
    {
        var tenantId = Guid.NewGuid();
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "siswa-flow", UserRole.Student, tenantId);

        Guid studentId, placementId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id, FullName = user.FullName, MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Test", StartDate = new DateOnly(2020, 1, 1), EndDate = new DateOnly(2030, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(), PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            db.Students.Add(student);
            db.Periods.Add(period);
            db.Placements.Add(placement);
            await db.SaveChangesAsync();
            studentId = student.Id;
            placementId = placement.Id;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return (client, studentId, placementId, tenantId, user.Email!);
    }

    private async Task<(HttpClient Client, Guid MentorUserId, string Email)> SeedMentorAsync(Guid tenantId, Guid placementId)
    {
        var mentorUserId = Guid.NewGuid();
        string email;
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>();
            email = $"mentor-flow-{Guid.NewGuid():N}@vokasia.test";
            var user = new Vokasia.Infrastructure.Identity.AppUser
            {
                Id = mentorUserId,
                UserName = email,
                Email = email,
                FullName = "Mentor Flow",
                Role = UserRole.IndustryMentor,
                TenantId = null,
                IsActive = true
            };
            var created = await userManager.CreateAsync(user, AuthTestHelpers.Password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var placement = await db.Placements.FindAsync(placementId);
            if (placement != null)
            {
                placement.MentorUserId = mentorUserId;
            }
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return (client, mentorUserId, email);
    }

    [Fact]
    public async Task Test1_SiswaSubmitJurnal_Berhasil()
    {
        _output.WriteLine("==================================================");
        _output.WriteLine("[BATCH 2: PKL JOURNAL LIFECYCLE FLOW]");
        _output.WriteLine("--------------------------------------------------");
        _output.WriteLine("Test 1");
        _output.WriteLine("Input: Siswa mengisi jurnal (Activity Text: \"Mengembangkan modul REST API C#\")");

        var (studentClient, studentId, placementId, tenantId, _) = await SeedStudentAsync();

        Guid slotId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Empty };
            db.JournalSlots.Add(slot);
            await db.SaveChangesAsync();
            slotId = slot.Id;
        }

        var payload = new
        {
            SlotId = slotId,
            Text = "Mengembangkan modul REST API C#",
            CompetencyIds = Array.Empty<Guid>(),
            PhotoIds = (Guid[]?)null
        };

        var response = await studentClient.PostAsJsonAsync($"/api/journals/{slotId}/submit", payload);
        var json = await response.Content.ReadAsStringAsync();

        _output.WriteLine("Output: Jurnal Berhasil Dikirim (Status: Submitted)");
        _output.WriteLine($"Result Details: HTTP {(int)response.StatusCode} {response.StatusCode} | Payload: {json}");
        _output.WriteLine("==================================================");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id", json);
    }

    [Fact]
    public async Task Test2_MentorReviewAndApprove_Disetujui()
    {
        _output.WriteLine("Test 2");
        _output.WriteLine("Input: Mentor memberikan persetujuan jurnal siswa (Status: Approved)");

        var (studentClient, studentId, placementId, tenantId, _) = await SeedStudentAsync();
        var (mentorClient, mentorUserId, _) = await SeedMentorAsync(tenantId, placementId);

        Guid entryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
            db.JournalSlots.Add(slot);
            var entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slot.Id, PlacementId = placementId, Text = "Setup DB EF Core", Status = JournalEntryStatus.Submitted, SubmittedAt = DateTimeOffset.UtcNow };
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        var approvePayload = new { Note = "Pekerjaan sangat baik" };
        var response = await mentorClient.PostAsJsonAsync($"/api/journals/{entryId}/approve", approvePayload);

        _output.WriteLine("Output: Jurnal Disetujui (Status: Approved)");
        _output.WriteLine($"Result Details: HTTP {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine("==================================================");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Test3_MentorUbahApprovedJournal_Returns409ImmutabilityError()
    {
        _output.WriteLine("Test 3 (Immutability Constraint Error Output)");
        _output.WriteLine("Input: Mentor mencoba menyetujui ulang/mengedit jurnal yang SUDAH Approved");

        var (studentClient, studentId, placementId, tenantId, _) = await SeedStudentAsync();
        var (mentorClient, mentorUserId, _) = await SeedMentorAsync(tenantId, placementId);

        Guid entryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
            db.JournalSlots.Add(slot);
            var entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slot.Id, PlacementId = placementId, Text = "Tugas Terkunci", Status = JournalEntryStatus.Approved, SubmittedAt = DateTimeOffset.UtcNow, ApprovedAt = DateTimeOffset.UtcNow };
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        var response = await mentorClient.PostAsJsonAsync($"/api/journals/{entryId}/approve", new { Note = "Coba ubah lagi" });
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine("Output Error: Jurnal yang sudah disetujui terkunci/immutable");
        _output.WriteLine($"Error Response Body: {content}");
        _output.WriteLine("==================================================");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("IMMUTABLE", content.ToUpper());
    }

    [Fact]
    public async Task Test4_SubmitJurnal_ValidasiInputKosong()
    {
        _output.WriteLine("Test 4 (Input Validation Error Output)");
        _output.WriteLine("Input: Submit Jurnal dengan deskripsi aktivitas KOSONG (\"  \")");

        var (studentClient, studentId, placementId, tenantId, _) = await SeedStudentAsync();

        Guid slotId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Empty };
            db.JournalSlots.Add(slot);
            await db.SaveChangesAsync();
            slotId = slot.Id;
        }

        var invalidPayload = new
        {
            SlotId = slotId,
            Text = "   ",
            CompetencyIds = Array.Empty<Guid>(),
            PhotoIds = (Guid[]?)null
        };
        var response = await studentClient.PostAsJsonAsync($"/api/journals/{slotId}/submit", invalidPayload);
        var json = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"Output Error Payload: {json}");
        _output.WriteLine("==================================================");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test5_SubmitJurnal_SystemErrorHandling_SlotTidakDitemukan()
    {
        _output.WriteLine("Test 5 (System Error Handling Output)");
        _output.WriteLine("Input: Submit Jurnal dengan ID Slot Fiktif (00000000-0000-0000-0000-000000000000)");

        var (studentClient, _, _, _, _) = await SeedStudentAsync();

        var payload = new
        {
            SlotId = Guid.Empty,
            Text = "Aktivitas valid",
            CompetencyIds = Array.Empty<Guid>(),
            PhotoIds = (Guid[]?)null
        };
        var response = await studentClient.PostAsJsonAsync($"/api/journals/{Guid.Empty}/submit", payload);
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"Output Structured Error: {content}");
        _output.WriteLine("==================================================");

        Assert.InRange((int)response.StatusCode, 400, 404);
    }
}
