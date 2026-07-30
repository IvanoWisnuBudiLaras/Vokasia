using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Tests.Integration;

/// <summary>
/// VOK-H6-E1 §2 — DUDI global registry (FR-SA-02). AC literal: "Given merge A→B, Then placement
/// pindah, riwayat tercatat, GET company A → redirect/flag merged."
/// </summary>
[Collection("IntegrationTests")]
public class SaCompaniesTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public SaCompaniesTests(VokasiaIntegrationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateCompany_ThenVerify_ReflectsInGetAndSearch()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-company-crud");

        var createResp = await saClient.PostAsJsonAsync("/sa/companies", new { Name = "PT Verifikasi Uji", Sector = "IT", City = "Surabaya", Address = (string?)null, ContactPerson = (string?)null });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var companyId = created.GetProperty("id").GetGuid();
        Assert.False(created.GetProperty("isVerified").GetBoolean());

        var searchResp = await saClient.GetAsync("/sa/companies/search?q=Verifikasi");
        Assert.Equal(HttpStatusCode.OK, searchResp.StatusCode);
        var searchBody = await searchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(searchBody.EnumerateArray(), e => e.GetProperty("id").GetGuid() == companyId);

        var verifyResp = await saClient.PostAsync($"/sa/companies/{companyId}/verify", null);
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
        var verified = await verifyResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(verified.GetProperty("isVerified").GetBoolean());

        var listResp = await saClient.GetAsync("/sa/companies?verified=true&search=Verifikasi");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var listBody = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(listBody.GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task MergeCompanies_MovesTenantCompanyPlacementAndSlot_RecordsHistoryAndFlagsSource()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-company-merge");
        var tenant = await _factory.SeedTenantAsync("SMK Merge Uji");

        Guid sourceId, targetId, placementId, slotId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var source = new Company { Id = Guid.NewGuid(), Name = "PT Duplikat Sumber" };
            var target = new Company { Id = Guid.NewGuid(), Name = "PT Duplikat Target" };
            db.Companies.Add(source);
            db.Companies.Add(target);
            db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.Id, CompanyId = source.Id });

            var period = new Period { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Periode Merge", StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), ClassLevels = "XII", Status = PeriodStatus.Active };
            var student = new Student { Id = Guid.NewGuid(), TenantId = tenant.Id, FullName = "Siswa Merge", MajorId = Guid.NewGuid(), Classroom = "XII A" };
            var placement = new Placement { Id = Guid.NewGuid(), TenantId = tenant.Id, StudentId = student.Id, CompanyId = source.Id, PeriodId = period.Id, TeacherId = Guid.NewGuid(), Status = PlacementStatus.Active };
            var slot = new CompanySlot { Id = Guid.NewGuid(), TenantId = tenant.Id, CompanyId = source.Id, PeriodId = period.Id, Slots = 2 };
            db.Periods.Add(period);
            db.Students.Add(student);
            db.Placements.Add(placement);
            db.CompanySlots.Add(slot);
            await db.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            placementId = placement.Id;
            slotId = slot.Id;
        }

        var mergeResp = await saClient.PostAsJsonAsync("/sa/companies/merge", new { SourceId = sourceId, TargetId = targetId });
        Assert.Equal(HttpStatusCode.OK, mergeResp.StatusCode);
        var mergeBody = await mergeResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, mergeBody.GetProperty("movedTenantCompanies").GetInt32());
        Assert.Equal(1, mergeBody.GetProperty("movedPlacements").GetInt32());

        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var placement = await db.Placements.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == placementId);
            Assert.Equal(targetId, placement.CompanyId);

            var slot = await db.CompanySlots.AsNoTracking().IgnoreQueryFilters().FirstAsync(s => s.Id == slotId);
            Assert.Equal(targetId, slot.CompanyId);

            var linked = await db.TenantCompanies.AsNoTracking().IgnoreQueryFilters()
                .AnyAsync(tc => tc.TenantId == tenant.Id && tc.CompanyId == targetId);
            Assert.True(linked);

            var history = await db.CompanyMergeHistories.AsNoTracking().FirstOrDefaultAsync(h => h.SourceCompanyId == sourceId && h.TargetCompanyId == targetId);
            Assert.NotNull(history);
            Assert.Contains("Duplikat Sumber", history!.SourceSnapshotJson);
        }

        // AC: "GET company A -> flag merged."
        var getSourceResp = await saClient.GetAsync($"/sa/companies/{sourceId}");
        Assert.Equal(HttpStatusCode.OK, getSourceResp.StatusCode);
        var getSourceBody = await getSourceResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(targetId, getSourceBody.GetProperty("mergedIntoId").GetGuid());

        // Company sudah merged tak boleh dimerge lagi (double-merge guard).
        var remergeResp = await saClient.PostAsJsonAsync("/sa/companies/merge", new { SourceId = sourceId, TargetId = targetId });
        Assert.Equal(HttpStatusCode.Conflict, remergeResp.StatusCode);

        // Company yang sudah merged TIDAK muncul di SearchCompanies (tak boleh di-link tenant baru).
        var searchResp = await saClient.GetAsync("/sa/companies/search?q=" + Uri.EscapeDataString("Duplikat Sumber"));
        var searchBody = await searchResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(searchBody.EnumerateArray(), e => e.GetProperty("id").GetGuid() == sourceId);
    }

    [Fact]
    public async Task MergeCompanies_WhenTargetHasSameTenantPeriodSlot_ReturnsConflictWithoutPartialChanges()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-company-merge-slot-collision");
        var tenant = await _factory.SeedTenantAsync("SMK Merge Konflik Slot");

        Guid sourceId, targetId, placementId, sourceSlotId, targetSlotId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var source = new Company { Id = Guid.NewGuid(), Name = "PT Slot Sumber" };
            var target = new Company { Id = Guid.NewGuid(), Name = "PT Slot Target" };
            var period = new Period
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "Periode Konflik Slot",
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31),
                ClassLevels = "XII",
                Status = PeriodStatus.Active,
            };
            var student = new Student
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                FullName = "Siswa Konflik Slot",
                MajorId = Guid.NewGuid(),
                Classroom = "XII A",
            };
            var placement = new Placement
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                StudentId = student.Id,
                CompanyId = source.Id,
                PeriodId = period.Id,
                TeacherId = Guid.NewGuid(),
                Status = PlacementStatus.Active,
            };
            var sourceSlot = new CompanySlot
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CompanyId = source.Id,
                PeriodId = period.Id,
                Slots = 2,
            };
            var targetSlot = new CompanySlot
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                CompanyId = target.Id,
                PeriodId = period.Id,
                Slots = 4,
            };

            db.AddRange(source, target, period, student, placement, sourceSlot, targetSlot);
            db.TenantCompanies.Add(new TenantCompany { TenantId = tenant.Id, CompanyId = source.Id });
            await db.SaveChangesAsync();

            sourceId = source.Id;
            targetId = target.Id;
            placementId = placement.Id;
            sourceSlotId = sourceSlot.Id;
            targetSlotId = targetSlot.Id;
        }

        var mergeResp = await saClient.PostAsJsonAsync(
            "/sa/companies/merge",
            new { SourceId = sourceId, TargetId = targetId });

        Assert.Equal(HttpStatusCode.Conflict, mergeResp.StatusCode);

        using var verifyScope = _factory.CreateDbScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var sourceAfter = await verifyDb.Companies.AsNoTracking().FirstAsync(c => c.Id == sourceId);
        var placementAfter = await verifyDb.Placements.AsNoTracking().IgnoreQueryFilters()
            .FirstAsync(p => p.Id == placementId);
        var sourceSlotAfter = await verifyDb.CompanySlots.AsNoTracking().IgnoreQueryFilters()
            .FirstAsync(s => s.Id == sourceSlotId);
        var targetSlotAfter = await verifyDb.CompanySlots.AsNoTracking().IgnoreQueryFilters()
            .FirstAsync(s => s.Id == targetSlotId);

        Assert.Null(sourceAfter.MergedIntoId);
        Assert.Equal(sourceId, placementAfter.CompanyId);
        Assert.Equal(sourceId, sourceSlotAfter.CompanyId);
        Assert.Equal(targetId, targetSlotAfter.CompanyId);
        Assert.True(await verifyDb.TenantCompanies.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(tc => tc.TenantId == tenant.Id && tc.CompanyId == sourceId));
        Assert.False(await verifyDb.TenantCompanies.AsNoTracking().IgnoreQueryFilters()
            .AnyAsync(tc => tc.TenantId == tenant.Id && tc.CompanyId == targetId));
        Assert.False(await verifyDb.CompanyMergeHistories.AsNoTracking()
            .AnyAsync(h => h.SourceCompanyId == sourceId && h.TargetCompanyId == targetId));
        Assert.False(await verifyDb.AuditLogs.AsNoTracking()
            .AnyAsync(log => log.Action == "CompanyMerged" && log.EntityId == sourceId.ToString()));
    }

    [Fact]
    public async Task MergeCompanies_WhenTargetWasAlreadyMerged_ReturnsConflictWithoutCreatingCycle()
    {
        var (_, saClient) = await _factory.LoginAsAsync(
            UserRole.SuperAdmin,
            null,
            "sa-company-merged-target");

        Guid sourceId, targetId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var source = new Company { Id = Guid.NewGuid(), Name = "PT Sumber Siklus" };
            var target = new Company
            {
                Id = Guid.NewGuid(),
                Name = "PT Target Hantu",
                MergedIntoId = source.Id,
            };
            db.Companies.AddRange(source, target);
            await db.SaveChangesAsync();
            sourceId = source.Id;
            targetId = target.Id;
        }

        var response = await saClient.PostAsJsonAsync(
            "/sa/companies/merge",
            new { SourceId = sourceId, TargetId = targetId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var verifyScope = _factory.CreateDbScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var sourceAfter = await verifyDb.Companies.AsNoTracking().FirstAsync(c => c.Id == sourceId);
        var targetAfter = await verifyDb.Companies.AsNoTracking().FirstAsync(c => c.Id == targetId);
        Assert.Null(sourceAfter.MergedIntoId);
        Assert.Equal(sourceId, targetAfter.MergedIntoId);
        Assert.False(await verifyDb.CompanyMergeHistories.AsNoTracking()
            .AnyAsync(h => h.SourceCompanyId == sourceId));
        Assert.False(await verifyDb.AuditLogs.AsNoTracking()
            .AnyAsync(log => log.Action == "CompanyMerged" && log.EntityId == sourceId.ToString()));
    }

    [Fact]
    public async Task MergeCompanies_ConcurrentRequestsForSameSource_OnlyOneCommits()
    {
        var (_, saClient) = await _factory.LoginAsAsync(
            UserRole.SuperAdmin,
            null,
            "sa-company-concurrent-merge");

        Guid sourceId, firstTargetId, secondTargetId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var source = new Company { Id = Guid.NewGuid(), Name = "PT Sumber Konkuren" };
            var firstTarget = new Company { Id = Guid.NewGuid(), Name = "PT Target Pertama" };
            var secondTarget = new Company { Id = Guid.NewGuid(), Name = "PT Target Kedua" };
            db.Companies.AddRange(source, firstTarget, secondTarget);
            await db.SaveChangesAsync();
            sourceId = source.Id;
            firstTargetId = firstTarget.Id;
            secondTargetId = secondTarget.Id;
        }

        var firstRequest = saClient.PostAsJsonAsync(
            "/sa/companies/merge",
            new { SourceId = sourceId, TargetId = firstTargetId });
        var secondRequest = saClient.PostAsJsonAsync(
            "/sa/companies/merge",
            new { SourceId = sourceId, TargetId = secondTargetId });
        var responses = await Task.WhenAll(firstRequest, secondRequest);

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        using var verifyScope = _factory.CreateDbScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        var sourceAfter = await verifyDb.Companies.AsNoTracking().FirstAsync(c => c.Id == sourceId);
        Assert.True(
            sourceAfter.MergedIntoId == firstTargetId ||
            sourceAfter.MergedIntoId == secondTargetId);
        Assert.Equal(
            1,
            await verifyDb.CompanyMergeHistories.AsNoTracking()
                .CountAsync(h => h.SourceCompanyId == sourceId));
        Assert.Equal(
            1,
            await verifyDb.AuditLogs.AsNoTracking()
                .CountAsync(log => log.Action == "CompanyMerged" && log.EntityId == sourceId.ToString()));
    }
}
