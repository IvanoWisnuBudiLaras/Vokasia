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

namespace Vokasia.Tests.Guard;

/// <summary>
/// AC VOK-H3-E3 §4 ImmutabilityTests: update/attach/delete pasca-Approved oleh siswa, mentor,
/// TenantAdmin -> semua ditolak (409 lewat DomainImmutableException+middleware kalau memang
/// mencapai guard; 403 kalau RBAC SUDAH memblokir lebih dulu sebelum sempat menyentuh entry -
/// AC sendiri eksplisit menulis "Then 409/403", KEDUANYA valid tergantung jalur). Rejected TETAP
/// bisa diisi ulang (BUKAN immutable) - dibuktikan juga di sini utk kontras eksplisit.
/// </summary>
public class ImmutabilityTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public ImmutabilityTests(VokasiaApiFactory factory) => _factory = factory;

    private sealed record Fixture(Guid TenantId, Guid StudentId, Guid PlacementId, Guid SlotId, Guid EntryId, Guid MentorUserId, Guid StudentUserId);

    private async Task<Fixture> SeedEntryAsync(JournalEntryStatus status)
    {
        var tenantId = Guid.NewGuid();
        var studentUser = await AuthTestHelpers.SeedUserAsync(_factory, "siswa-immut", UserRole.Student, tenantId);
        var mentorUser = await AuthTestHelpers.SeedUserAsync(_factory, "mentor-immut", UserRole.IndustryMentor, null);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

        var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = studentUser.Id, FullName = studentUser.FullName, MajorId = Guid.NewGuid(), Classroom = "XII" };
        var placement = new Placement
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(),
            PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), MentorUserId = mentorUser.Id, Status = PlacementStatus.Active,
        };
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slot.Id, PlacementId = placement.Id,
            Text = "Versi awal.", Status = status,
            ApprovedAt = status == JournalEntryStatus.Approved ? DateTimeOffset.UtcNow : null,
        };

        db.Students.Add(student);
        db.Placements.Add(placement);
        db.JournalSlots.Add(slot);
        db.JournalEntries.Add(entry);
        await db.SaveChangesAsync();

        return new Fixture(tenantId, student.Id, placement.Id, slot.Id, entry.Id, mentorUser.Id, studentUser.Id);
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task Student_ResubmitOnApprovedEntry_Returns409WithImmutableCode()
    {
        var fx = await SeedEntryAsync(JournalEntryStatus.Approved);
        using var scope = _factory.Services.CreateScope();
        var studentUser = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>().FindByIdAsync(fx.StudentUserId.ToString());
        var client = await AuthenticatedClientAsync(studentUser!.Email!);

        var resp = await client.PostAsJsonAsync($"/api/journals/{fx.SlotId}/submit",
            new { SlotId = fx.SlotId, Text = "Coba ubah pasca-approve.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("journal-approved-immutable", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Student_AttachPhotoOnApprovedEntry_Returns409WithImmutableCode()
    {
        // PROMPT-D: dicoba dulu TANPA baris `entry.EnsureMutable()` di AttachPhoto (JournalEndpoints.cs)
        // -> test ini MERAH (200 Created, bukan 409) - persis prediksi (gap nyata, bukan dugaan).
        // Baris dikembalikan -> HIJAU. Lihat komentar PROMPT-D di AttachPhoto sendiri.
        var fx = await SeedEntryAsync(JournalEntryStatus.Approved);
        using var scope = _factory.Services.CreateScope();
        var studentUser = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>().FindByIdAsync(fx.StudentUserId.ToString());
        var client = await AuthenticatedClientAsync(studentUser!.Email!);

        var resp = await client.PostAsJsonAsync($"/api/journals/{fx.EntryId}/photos", new { ObjectKey = "tenant/x/journal/coba.jpg" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("journal-approved-immutable", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Mentor_ReApproveOnApprovedEntry_Returns409WithImmutableCode()
    {
        var fx = await SeedEntryAsync(JournalEntryStatus.Approved);
        using var scope = _factory.Services.CreateScope();
        var mentorUser = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>().FindByIdAsync(fx.MentorUserId.ToString());
        var client = await AuthenticatedClientAsync(mentorUser!.Email!);

        var resp = await client.PostAsJsonAsync($"/api/journals/{fx.EntryId}/approve", new { Note = (string?)null });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("journal-approved-immutable", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Mentor_RejectOnApprovedEntry_Returns409WithImmutableCode()
    {
        var fx = await SeedEntryAsync(JournalEntryStatus.Approved);
        using var scope = _factory.Services.CreateScope();
        var mentorUser = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>().FindByIdAsync(fx.MentorUserId.ToString());
        var client = await AuthenticatedClientAsync(mentorUser!.Email!);

        var resp = await client.PostAsJsonAsync($"/api/journals/{fx.EntryId}/reject", new { Reason = "Alasan penolakan valid." });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("journal-approved-immutable", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TenantAdmin_ApproveOnApprovedEntry_IsRejectedByRbacBeforeReachingEntry()
    {
        // TenantAdmin TIDAK punya endpoint khusus utk mengubah jurnal di scope H1-H3 - jalur satu2nya
        // yang ada adalah endpoint mentor (ApproveJournal), yang policy MentorOwnPlacement-nya
        // SUDAH menolak non-IndustryMentor SEBELUM sempat menyentuh status entry sama sekali. Ini
        // TETAP bukti sah "tak bisa diubah" (AC sendiri: "Then 409/403" - keduanya valid) - defense
        // in depth: bahkan TANPA guard immutability, RBAC saja sudah cukup menutup jalur ini utk
        // role TenantAdmin.
        var fx = await SeedEntryAsync(JournalEntryStatus.Approved);
        var tenantAdmin = await AuthTestHelpers.SeedUserAsync(_factory, "admin-immut", UserRole.TenantAdmin, fx.TenantId);
        var client = await AuthenticatedClientAsync(tenantAdmin.Email!);

        var resp = await client.PostAsJsonAsync($"/api/journals/{fx.EntryId}/approve", new { Note = (string?)null });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Student_ResubmitOnRejectedEntry_SucceedsBecauseNotImmutable()
    {
        var fx = await SeedEntryAsync(JournalEntryStatus.Rejected);
        using var scope = _factory.Services.CreateScope();
        var studentUser = await scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Vokasia.Infrastructure.Identity.AppUser>>().FindByIdAsync(fx.StudentUserId.ToString());
        var client = await AuthenticatedClientAsync(studentUser!.Email!);

        var resp = await client.PostAsJsonAsync($"/api/journals/{fx.SlotId}/submit",
            new { SlotId = fx.SlotId, Text = "Revisi setelah ditolak.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task BatchApprove_OneItemAlreadyApproved_OthersStillSucceed()
    {
        // AC "satu gagal tak membatalkan lainnya" - dibuktikan SPESIFIK utk kasus immutable (bukan
        // cuma "not-own-placement"/"not-found" yg sudah dites JournalMentorEndpointsTests.cs H3-E1).
        var mentorUser = await AuthTestHelpers.SeedUserAsync(_factory, "mentor-batch-immut", UserRole.IndustryMentor, null);
        var client = await AuthenticatedClientAsync(mentorUser.Email!);

        Guid approvedEntryId, pendingEntryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();

            var tenantId = Guid.NewGuid();
            var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), MentorUserId = mentorUser.Id, Status = PlacementStatus.Active };
            db.Placements.Add(placement);

            var approvedSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = AppTimeZone.TodayJakarta().AddDays(-1), Status = JournalSlotStatus.Filled };
            var approvedEntry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = approvedSlot.Id, PlacementId = placement.Id, Text = "Sudah disetujui.", Status = JournalEntryStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow };
            db.JournalSlots.Add(approvedSlot);
            db.JournalEntries.Add(approvedEntry);

            var pendingSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placement.Id, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
            var pendingEntry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = pendingSlot.Id, PlacementId = placement.Id, Text = "Menunggu persetujuan.", Status = JournalEntryStatus.Submitted };
            db.JournalSlots.Add(pendingSlot);
            db.JournalEntries.Add(pendingEntry);

            await db.SaveChangesAsync();
            approvedEntryId = approvedEntry.Id;
            pendingEntryId = pendingEntry.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/journals/batch-approve", new { Ids = new[] { approvedEntryId, pendingEntryId } });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var approved = body.GetProperty("approved");
        var failed = body.GetProperty("failed");
        Assert.Equal(1, approved.GetArrayLength());
        Assert.Equal(pendingEntryId, approved[0].GetGuid());
        Assert.Equal(1, failed.GetArrayLength());
        Assert.Equal(approvedEntryId, failed[0].GetProperty("id").GetGuid());
    }
}
