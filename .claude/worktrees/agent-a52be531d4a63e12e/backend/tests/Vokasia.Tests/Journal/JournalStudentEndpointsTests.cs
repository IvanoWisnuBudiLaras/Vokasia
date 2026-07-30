using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Tests.Auth;

namespace Vokasia.Tests.Journal;

/// <summary>
/// AC VOK-H3-E1 §2 (endpoint siswa, policy StudentSelf) via HTTP Bearer sungguhan (dance PKCE
/// penuh, sama disiplin RbacPolicyTests) — bukan cuma panggil handler in-process, krn StudentSelf
/// adalah policy CLAIM-ONLY (bukan resource-scoped spt MentorOwnPlacement), jadi endpoint-nya
/// SENDIRI yang wajib menyaring "punya sendiri" — ini justru bagian yang paling penting dibuktikan
/// lewat request sungguhan, bukan diasumsikan dari baca kode.
/// </summary>
public class JournalStudentEndpointsTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    public JournalStudentEndpointsTests(VokasiaApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, Guid StudentId, Guid PlacementId, Guid PeriodId, Guid MajorId, Guid TenantId)> SeedStudentWithActivePlacementAsync()
    {
        var tenantId = Guid.NewGuid();
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "siswa", UserRole.Student, tenantId);

        Guid studentId, placementId, periodId, majorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            majorId = Guid.NewGuid();
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id, FullName = user.FullName, MajorId = majorId, Classroom = "XII RPL 1" };
            var period = new Period { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Periode Uji", StartDate = new DateOnly(2020, 1, 1), EndDate = new DateOnly(2030, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenantId, StudentId = student.Id, CompanyId = Guid.NewGuid(), PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            db.Students.Add(student);
            db.Periods.Add(period);
            db.Placements.Add(placement);
            await db.SaveChangesAsync();
            studentId = student.Id;
            placementId = placement.Id;
            periodId = period.Id;
        }

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (accessToken, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return (client, studentId, placementId, periodId, majorId, tenantId);
    }

    private async Task<Guid> SeedTodaySlotAsync(Guid placementId, Guid tenantId, JournalSlotStatus status = JournalSlotStatus.Empty)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var slot = new JournalSlot { Id = Guid.NewGuid(), TenantId = tenantId, PlacementId = placementId, Date = AppTimeZone.TodayJakarta(), Status = status };
        db.JournalSlots.Add(slot);
        await db.SaveChangesAsync();
        return slot.Id;
    }

    [Fact]
    public async Task GetTodayJournal_NoSlotYet_Returns404()
    {
        var (client, _, _, _, _, _) = await SeedStudentWithActivePlacementAsync();

        var resp = await client.GetAsync("/api/journals/today");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetTodayJournal_SlotExistsNoEntry_ReturnsSlotWithNullEntry()
    {
        var (client, _, placementId, _, majorId, tenantId) = await SeedStudentWithActivePlacementAsync();
        await SeedTodaySlotAsync(placementId, tenantId);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            db.Competencies.Add(new Competency { Id = Guid.NewGuid(), TenantId = tenantId, MajorId = majorId, Name = "Pemrograman Web" });
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync("/api/journals/today");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        // Enum diserialisasi sbg angka (tak ada JsonStringEnumConverter global dikonfigurasi di
        // proyek ini) - bandingkan int, bukan string, sesuai perilaku nyata (dibuktikan lewat
        // InvalidOperationException "Number bukan String" saat pertama kali dicoba .GetString()).
        Assert.Equal((int)JournalSlotStatus.Empty, body.GetProperty("slot").GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("entry").ValueKind);
        Assert.Equal(1, body.GetProperty("competencies").GetArrayLength());
        Assert.Equal(5, body.GetProperty("weekStatus").GetArrayLength());
    }

    [Fact]
    public async Task SubmitJournal_HappyPath_CreatesEntryAndFillsSlotAndPublishesOutbox()
    {
        var (client, _, placementId, _, majorId, tenantId) = await SeedStudentWithActivePlacementAsync();
        var slotId = await SeedTodaySlotAsync(placementId, tenantId);

        // VOK-H3-E3 §2: SubmitJournalValidator SEKARANG memverifikasi CompetencyIds benar2 milik
        // major siswa pemanggil (async, query Competencies) - guid acak tanpa baris Competency
        // sungguhan (perilaku test lama) kini DITOLAK 400 by design. Seed baris nyata dgn MajorId
        // yang sama persis dgn siswa ini, supaya "happy path" tetap benar2 valid di bawah aturan baru.
        Guid competencyId;
        using (var scope = _factory.Services.CreateScope())
        {
            var seedDb = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var competency = new Competency { Id = Guid.NewGuid(), TenantId = tenantId, MajorId = majorId, Name = "Pemrograman Web" };
            seedDb.Competencies.Add(competency);
            await seedDb.SaveChangesAsync();
            competencyId = competency.Id;
        }

        int outboxBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            outboxBefore = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>().OutboxMessages.Count();
        }

        var resp = await client.PostAsJsonAsync($"/api/journals/{slotId}/submit", new
        {
            SlotId = slotId,
            Text = "Hari ini belajar setup CI/CD.",
            CompetencyIds = new[] { competencyId },
            PhotoIds = (Guid[]?)null,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)JournalEntryStatus.Submitted, body.GetProperty("status").GetInt32());

        using var verify = _factory.Services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var slot = await db.JournalSlots.FirstAsync(s => s.Id == slotId);
        Assert.Equal(JournalSlotStatus.Filled, slot.Status);
        Assert.Equal(outboxBefore + 1, db.OutboxMessages.Count());
    }

    [Fact]
    public async Task SubmitJournal_TextTooLong_Returns400()
    {
        var (client, _, placementId, _, _, tenantId) = await SeedStudentWithActivePlacementAsync();
        var slotId = await SeedTodaySlotAsync(placementId, tenantId);

        var resp = await client.PostAsJsonAsync($"/api/journals/{slotId}/submit", new
        {
            SlotId = slotId,
            Text = new string('a', 501),
            CompetencyIds = Array.Empty<Guid>(),
            PhotoIds = (Guid[]?)null,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SubmitJournal_SlotAlreadySubmitted_Returns409()
    {
        var (client, _, placementId, _, _, tenantId) = await SeedStudentWithActivePlacementAsync();
        var slotId = await SeedTodaySlotAsync(placementId, tenantId);
        var body = new { SlotId = slotId, Text = "Isi pertama.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null };
        var first = await client.PostAsJsonAsync($"/api/journals/{slotId}/submit", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync($"/api/journals/{slotId}/submit", new { SlotId = slotId, Text = "Coba isi lagi.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task SubmitJournal_SlotBelongsToAnotherPlacement_Returns403()
    {
        var (client, _, _, _, _, _) = await SeedStudentWithActivePlacementAsync();
        // Slot milik placement ORANG LAIN (seed langsung, tak lewat client ini).
        Guid otherSlotId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var otherTenant = Guid.NewGuid();
            var otherPlacement = new Placement { Id = Guid.NewGuid(), TenantId = otherTenant, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var otherSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = otherTenant, PlacementId = otherPlacement.Id, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Empty };
            db.Placements.Add(otherPlacement);
            db.JournalSlots.Add(otherSlot);
            await db.SaveChangesAsync();
            otherSlotId = otherSlot.Id;
        }

        var resp = await client.PostAsJsonAsync($"/api/journals/{otherSlotId}/submit", new { SlotId = otherSlotId, Text = "Coba curi slot orang.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task SubmitJournal_ResubmitAfterRejected_Succeeds()
    {
        var (client, _, placementId, _, _, tenantId) = await SeedStudentWithActivePlacementAsync();
        var slotId = await SeedTodaySlotAsync(placementId, tenantId, JournalSlotStatus.Filled);
        Guid entryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var entry = new JournalEntry { Id = Guid.NewGuid(), TenantId = tenantId, SlotId = slotId, PlacementId = placementId, Text = "Versi ditolak.", Status = JournalEntryStatus.Rejected, MentorNote = "Kurang detail." };
            db.JournalEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        var resp = await client.PostAsJsonAsync($"/api/journals/{slotId}/submit", new { SlotId = slotId, Text = "Versi revisi, lebih detail.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var verify = _factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var entryAfter = await verifyDb.JournalEntries.FirstAsync(e => e.Id == entryId);
        Assert.Equal(JournalEntryStatus.Submitted, entryAfter.Status);
        Assert.Equal("Versi revisi, lebih detail.", entryAfter.Text);
        Assert.Null(entryAfter.MentorNote);
    }

    [Fact]
    public async Task GetPresignedUploadUrl_ValidRequest_ReturnsUrlWithTenantPrefix()
    {
        var (client, _, _, _, _, tenantId) = await SeedStudentWithActivePlacementAsync();

        var resp = await client.PostAsJsonAsync("/api/journals/upload-url", new { FileName = "foto.jpg", ContentType = "image/jpeg", SizeBytes = 1024 * 100 });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var objectKey = body.GetProperty("objectKey").GetString()!;
        Assert.StartsWith($"tenant/{tenantId}/journal/", objectKey);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("uploadUrl").GetString()));
    }

    [Fact]
    public async Task GetPresignedUploadUrl_DisallowedContentType_Returns400()
    {
        var (client, _, _, _, _, _) = await SeedStudentWithActivePlacementAsync();

        var resp = await client.PostAsJsonAsync("/api/journals/upload-url", new { FileName = "foto.gif", ContentType = "image/gif", SizeBytes = 1024 });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task GetPresignedUploadUrl_TooLarge_Returns400()
    {
        var (client, _, _, _, _, _) = await SeedStudentWithActivePlacementAsync();

        var resp = await client.PostAsJsonAsync("/api/journals/upload-url", new { FileName = "foto.jpg", ContentType = "image/jpeg", SizeBytes = 6L * 1024 * 1024 });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AttachPhoto_FourthPhoto_Returns409()
    {
        var (client, _, placementId, _, _, tenantId) = await SeedStudentWithActivePlacementAsync();
        var slotId = await SeedTodaySlotAsync(placementId, tenantId);
        var submitResp = await client.PostAsJsonAsync($"/api/journals/{slotId}/submit", new { SlotId = slotId, Text = "Isi dgn foto.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null });
        var entryId = (await submitResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        for (var i = 0; i < 3; i++)
        {
            var r = await client.PostAsJsonAsync($"/api/journals/{entryId}/photos", new { ObjectKey = $"tenant/x/journal/foto-{i}.jpg" });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        }

        var fourth = await client.PostAsJsonAsync($"/api/journals/{entryId}/photos", new { ObjectKey = "tenant/x/journal/foto-4.jpg" });

        Assert.Equal(HttpStatusCode.Conflict, fourth.StatusCode);
    }

    [Fact]
    public async Task ListJournals_OnlyReturnsOwnPlacementJournals()
    {
        var (client, _, placementId, _, _, tenantId) = await SeedStudentWithActivePlacementAsync();
        var slotId = await SeedTodaySlotAsync(placementId, tenantId);
        await client.PostAsJsonAsync($"/api/journals/{slotId}/submit", new { SlotId = slotId, Text = "Punya sendiri.", CompetencyIds = Array.Empty<Guid>(), PhotoIds = (Guid[]?)null });

        // Jurnal milik siswa/placement LAIN - tak boleh ikut muncul.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var otherTenant = Guid.NewGuid();
            var otherPlacement = new Placement { Id = Guid.NewGuid(), TenantId = otherTenant, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var otherSlot = new JournalSlot { Id = Guid.NewGuid(), TenantId = otherTenant, PlacementId = otherPlacement.Id, Date = AppTimeZone.TodayJakarta(), Status = JournalSlotStatus.Filled };
            var otherEntry = new JournalEntry { Id = Guid.NewGuid(), TenantId = otherTenant, SlotId = otherSlot.Id, PlacementId = otherPlacement.Id, Text = "Punya orang lain.", Status = JournalEntryStatus.Submitted };
            db.Placements.Add(otherPlacement);
            db.JournalSlots.Add(otherSlot);
            db.JournalEntries.Add(otherEntry);
            await db.SaveChangesAsync();
        }

        var resp = await client.GetAsync("/api/journals/");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal("Punya sendiri.", items[0].GetProperty("text").GetString());
    }
}
