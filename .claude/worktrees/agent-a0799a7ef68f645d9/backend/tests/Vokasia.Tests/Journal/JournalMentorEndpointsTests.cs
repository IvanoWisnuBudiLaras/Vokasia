using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Journal;

/// <summary>
/// AC VOK-H3-E1 §3 (endpoint mentor, policy MentorOwnPlacement). PENTING: ini pemakai HTTP NYATA
/// PERTAMA utk <c>PlacementScopeHandler</c> (resource-based) — sebelumnya cuma diuji lewat unit
/// test langsung thd handler (RbacPolicyTests), belum pernah dibuktikan lewat request sungguhan yg
/// benar2 melewati pipeline ASP.NET Core authorization + endpoint. Test
/// <see cref="ApproveJournal_MentorDoesNotOwnPlacement_Returns403"/> secara khusus membuktikan
/// integrasi route-level ".RequireAuthorization()" (bare) + in-handler
/// "authService.AuthorizeAsync(user, placement, MentorOwnPlacement)" benar2 menolak mentor yg
/// bukan pemilik — kalau integrasi ini salah (mis. lupa panggil AuthorizeAsync sama sekali), test
/// ini akan gagal (200 bukannya 403), bukan cuma lolos diam2 krn cek statis kode.
/// </summary>
public class JournalMentorEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public JournalMentorEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    /// <summary>
    /// Mentor TenantId=null (lintas-tenant by design, VOK-H2-E3) - AppUser di-seed dgn Id SPESIFIK
    /// (bukan lewat AuthTestHelpers.SeedUserAsync yg generate Id sendiri via UserManager) krn Id
    /// itu harus cocok persis dgn Placement.MentorUserId yang di-seed manual di test.
    /// </summary>
    private async Task<(HttpClient Client, Guid MentorUserId)> SeedMentorClientAsync(string emailLocalPart = "mentor")
    {
        var mentorUserId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>();
            var email = $"{emailLocalPart}-{Guid.NewGuid():N}@vokasia.test";
            var user = new Vokasia.Infrastructure.Identity.AppUser
            {
                Id = mentorUserId,
                UserName = email,
                Email = email,
                FullName = "Test " + emailLocalPart,
                Role = UserRole.IndustryMentor,
                TenantId = null,
                IsActive = true,
            };
            var created = await userManager.CreateAsync(user, AuthTestHelpers.Password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        }

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>();
            var user = await userManager.FindByIdAsync(mentorUserId.ToString());
            var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user!.Email!);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return (client, mentorUserId);
    }

    private async Task<(Guid EntryId, Guid PlacementId)> SeedSubmittedEntryForMentorAsync(Guid mentorUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var tenantId = Guid.NewGuid();
        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, FullName = "Siswa Bimbingan", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
        var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), MentorUserId = mentorUserId, Status = PlacementStatus.Active };
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
        var entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slot.Id, PlacementId = placement.Id, Text = "Jurnal menunggu approval.", Status = JournalEntryStatus.Submitted };
        db.Students.Add(student);
        db.Placements.Add(placement);
        db.JournalSlots.Add(slot);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();
        return (entry.Id, placement.Id);
    }

    [Fact]
    public async Task ApproveJournal_MentorOwnsPlacement_Succeeds()
    {
        var (client, mentorId) = await SeedMentorClientAsync();
        var (entryId, _) = await SeedSubmittedEntryForMentorAsync(mentorId);
        int outboxBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            outboxBefore = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>().OutboxMessages.Count();
        }

        var resp = await client.PostAsJsonAsync($"/api/journals/{entryId}/approve", new { Note = "Bagus, lanjutkan." });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var entry = await db.JournalEntries.FirstAsync(e => e.Id == entryId);
        Assert.Equal(JournalEntryStatus.Approved, entry.Status);
        Assert.NotNull(entry.ApprovedAt);
        Assert.Equal(outboxBefore + 1, db.OutboxMessages.Count());
    }

    [Fact]
    public async Task ApproveJournal_MentorDoesNotOwnPlacement_Returns403()
    {
        var (client, _) = await SeedMentorClientAsync("mentor-a");
        var (_, otherMentorId) = (Guid.Empty, Guid.NewGuid()); // mentor LAIN, bukan yg login.
        var (entryId, _) = await SeedSubmittedEntryForMentorAsync(otherMentorId);

        var resp = await client.PostAsJsonAsync($"/api/journals/{entryId}/approve", new { Note = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var entry = await db.JournalEntries.FirstAsync(e => e.Id == entryId);
        Assert.Equal(JournalEntryStatus.Submitted, entry.Status); // tak berubah sama sekali.
    }

    [Fact]
    public async Task ApproveJournal_AlreadyApproved_Returns409()
    {
        var (client, mentorId) = await SeedMentorClientAsync();
        var (entryId, _) = await SeedSubmittedEntryForMentorAsync(mentorId);
        var first = await client.PostAsJsonAsync($"/api/journals/{entryId}/approve", new { Note = (string?)null });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/journals/{entryId}/approve", new { Note = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RejectJournal_ValidReason_Succeeds()
    {
        var (client, mentorId) = await SeedMentorClientAsync();
        var (entryId, _) = await SeedSubmittedEntryForMentorAsync(mentorId);

        var resp = await client.PostAsJsonAsync($"/api/journals/{entryId}/reject", new { Reason = "Kurang detail, tolong revisi." });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var entry = await db.JournalEntries.FirstAsync(e => e.Id == entryId);
        Assert.Equal(JournalEntryStatus.Rejected, entry.Status);
        Assert.Equal("Kurang detail, tolong revisi.", entry.MentorNote);
    }

    [Fact]
    public async Task RejectJournal_EmptyReason_Returns400()
    {
        var (client, mentorId) = await SeedMentorClientAsync();
        var (entryId, _) = await SeedSubmittedEntryForMentorAsync(mentorId);

        var resp = await client.PostAsJsonAsync($"/api/journals/{entryId}/reject", new { Reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task RejectJournal_MentorDoesNotOwnPlacement_Returns403()
    {
        var (client, _) = await SeedMentorClientAsync();
        var (entryId, _) = await SeedSubmittedEntryForMentorAsync(Guid.NewGuid());

        var resp = await client.PostAsJsonAsync($"/api/journals/{entryId}/reject", new { Reason = "Coba tolak punya orang." });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task BatchApprove_TenPendingAllOwn_AllApprovedWithTenOutboxEvents()
    {
        // AC literal: "Given 10 pending, When BatchApprove, Then semua Approved + 10 event outbox."
        var (client, mentorId) = await SeedMentorClientAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var (entryId, _) = await SeedSubmittedEntryForMentorAsync(mentorId);
            ids.Add(entryId);
        }
        int outboxBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            outboxBefore = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>().OutboxMessages.Count();
        }

        var resp = await client.PostAsJsonAsync("/api/journals/batch-approve", new { Ids = ids });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10, body.GetProperty("approved").GetArrayLength());
        Assert.Equal(0, body.GetProperty("failed").GetArrayLength());

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        foreach (var id in ids)
        {
            Assert.Equal(JournalEntryStatus.Approved, (await db.JournalEntries.FirstAsync(e => e.Id == id)).Status);
        }
        Assert.Equal(outboxBefore + 10, db.OutboxMessages.Count());
    }

    [Fact]
    public async Task BatchApprove_MixedOwnership_ApprovesOwnFailsOthersIndependently()
    {
        var (client, mentorId) = await SeedMentorClientAsync();
        var (ownEntry1, _) = await SeedSubmittedEntryForMentorAsync(mentorId);
        var (ownEntry2, _) = await SeedSubmittedEntryForMentorAsync(mentorId);
        var (notOwnEntry, _) = await SeedSubmittedEntryForMentorAsync(Guid.NewGuid());

        var resp = await client.PostAsJsonAsync("/api/journals/batch-approve", new { Ids = new[] { ownEntry1, ownEntry2, notOwnEntry } });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, body.GetProperty("approved").GetArrayLength());
        Assert.Equal(1, body.GetProperty("failed").GetArrayLength());

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        Assert.Equal(JournalEntryStatus.Approved, (await db.JournalEntries.FirstAsync(e => e.Id == ownEntry1)).Status);
        Assert.Equal(JournalEntryStatus.Approved, (await db.JournalEntries.FirstAsync(e => e.Id == ownEntry2)).Status);
        Assert.Equal(JournalEntryStatus.Submitted, (await db.JournalEntries.FirstAsync(e => e.Id == notOwnEntry)).Status); // tak tersentuh.
    }

    [Fact]
    public async Task GetPendingApprovals_OnlyReturnsOwnMentoredStudents()
    {
        var (client, mentorId) = await SeedMentorClientAsync();
        await SeedSubmittedEntryForMentorAsync(mentorId);
        await SeedSubmittedEntryForMentorAsync(Guid.NewGuid()); // milik mentor lain, tak boleh muncul.

        var resp = await client.GetAsync("/api/journals/pending");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetArrayLength());
    }

    [Fact]
    public async Task AddTeacherComment_ValidRequest_CreatesCommentAndNotifiesStudent()
    {
        var teacherTenantId = Guid.NewGuid();
        var teacherUser = await AuthTestHelpers.SeedUserAsync(_factory, "guru", UserRole.Teacher, teacherTenantId);
        var studentUserId = Guid.NewGuid();
        Guid entryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var student = new Student { Id = Guid.NewGuid(), TenantId = teacherTenantId, UserId = studentUserId, FullName = "Siswa Dikomentari", MajorId = Guid.NewGuid(), Classroom = "XII RPL 1" };
            var placement = new Placement { Id = Guid.NewGuid(), TenantId = teacherTenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = teacherTenantId, PlacementId = placement.Id, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
            var entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = teacherTenantId, SlotId = slot.Id, PlacementId = placement.Id, Text = "Jurnal utk dikomentari.", Status = JournalEntryStatus.Approved };
            db.Students.Add(student);
            db.Placements.Add(placement);
            db.JournalSlots.Add(slot);
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, teacherUser.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var resp = await client.PostAsJsonAsync($"/api/journals/{entryId}/comments", new { Text = "Pertahankan konsistensi ini." });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        Assert.Single(verifyDb.TeacherComments.Where(c => c.JournalEntryId == entryId));
        Assert.Single(verifyDb.Notifications.Where(n => n.UserId == studentUserId && n.Type == "TeacherComment"));
    }
}
