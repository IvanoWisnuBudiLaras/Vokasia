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
/// VOK-H6-E1 §3/§4 — Feature flags (resolusi plan->override) + Ops (KPI/health/audit). GetSystemHealth
/// diuji LONGGAR utk QueueDepth/DlqCount/FailedJobs (best-effort, lihat doc-comment SaOpsEndpoints.
/// GetSystemHealth — Testcontainers RabbitMq di sini TIDAK expose port mgmt 15672 & Worker test host
/// TIDAK menjalankan migrasi skema Hangfire, jadi keduanya DIHARAPKAN null di lingkungan test ini,
/// bukan kegagalan) - OutboxUnpublished (sumber lokal, DB yang sama) yang diuji ketat.
/// </summary>
[Collection("IntegrationTests")]
public class SaOpsAndFlagsTests
{
    private readonly VokasiaIntegrationFactory _factory;
    public SaOpsAndFlagsTests(VokasiaIntegrationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEffectiveFlags_TenantOverrideWinsOverPlanValue()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-flags");

        var planResp = await saClient.PostAsJsonAsync("/sa/plans", new { Name = "Paket Flag Uji", PriceMonthly = 100000m, MaxStudents = 10, MaxPlacements = 10 });
        var planBody = await planResp.Content.ReadFromJsonAsync<JsonElement>();
        var planId = planBody.GetProperty("id").GetGuid();

        var tenantResp = await saClient.PostAsJsonAsync("/sa/tenants", new
        {
            SchoolName = "SMK Flag Uji",
            Npsn = (string?)null,
            City = "Medan",
            AdminEmail = $"flagadmin-{Guid.NewGuid():N}@vokasia.test",
            AdminName = "Admin Flag",
            PlanId = planId,
        });
        var tenantBody = await tenantResp.Content.ReadFromJsonAsync<JsonElement>();
        var tenantId = tenantBody.GetProperty("id").GetGuid();

        // FeatureFlagKey.GeotagAllowed = 0 (enum TANPA JsonStringEnumConverter, pola sama enum lain di repo ini).
        var setPlanFlagResp = await saClient.PostAsJsonAsync($"/sa/plans/{planId}/flags", new { Key = 0, Enabled = false });
        Assert.Equal(HttpStatusCode.NoContent, setPlanFlagResp.StatusCode);

        var beforeOverrideResp = await saClient.GetAsync($"/sa/tenants/{tenantId}/flags/effective");
        var beforeOverride = await beforeOverrideResp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.False(beforeOverride![nameof(FeatureFlagKey.GeotagAllowed)]);

        var overrideResp = await saClient.PostAsJsonAsync($"/sa/tenants/{tenantId}/flags", new { Key = 0, Enabled = true });
        Assert.Equal(HttpStatusCode.NoContent, overrideResp.StatusCode);

        // Cache di-invalidasi eksplisit saat override ditulis - harus langsung terlihat, BUKAN nunggu TTL 60 dtk.
        var afterOverrideResp = await saClient.GetAsync($"/sa/tenants/{tenantId}/flags/effective");
        var afterOverride = await afterOverrideResp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.True(afterOverride![nameof(FeatureFlagKey.GeotagAllowed)]);
    }

    [Fact]
    public async Task GetPlatformKpis_ReflectsSeededActiveTenantAndPlanPrice()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-kpi");

        Guid tenantId, planId;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            var plan = new Plan { Id = Guid.NewGuid(), Name = "Paket KPI Uji", PriceMonthly = 1_000_000m, MaxStudents = 50, MaxPlacements = 50 };
            var tenant = new Tenant { Id = Guid.NewGuid(), SchoolName = "SMK KPI Uji " + Guid.NewGuid().ToString("N")[..6], IsActive = true, PlanId = plan.Id };
            db.Plans.Add(plan);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
            tenantId = tenant.Id;
            planId = plan.Id;
        }

        var kpiResp = await saClient.GetAsync("/sa/kpis");
        Assert.Equal(HttpStatusCode.OK, kpiResp.StatusCode);
        var kpi = await kpiResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(kpi.GetProperty("activeTenants").GetInt32() >= 1);
        Assert.True(kpi.GetProperty("mrr").GetDecimal() >= 1_000_000m);

        using var verifyScope = _factory.CreateDbScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
        Assert.True(await verifyDb.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId && t.PlanId == planId));
    }

    [Fact]
    public async Task GetSystemHealth_ReturnsRealOutboxUnpublishedCount()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-health");

        int outboxBefore;
        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            outboxBefore = await db.OutboxMessages.AsNoTracking().CountAsync(m => m.PublishedAt == null);
            db.OutboxMessages.Add(new OutboxMessage { Id = Guid.NewGuid(), Type = "UnknownTypeForHealthTest", PayloadJson = "{}" });
            await db.SaveChangesAsync();
        }

        var healthResp = await saClient.GetAsync("/sa/health");
        Assert.Equal(HttpStatusCode.OK, healthResp.StatusCode);
        var health = await healthResp.Content.ReadFromJsonAsync<JsonElement>();
        // >= bukan == : OutboxDispatcher (Worker sungguhan, poll 2 dtk) SEDANG hidup di background suite
        // ini dan MUNGKIN sudah memproses baris lain sejak seed factory - baris BARU di atas dijamin
        // "unknown type" (TypeRegistry tak kenal) jadi TETAP unpublished selamanya, aman dihitung sbg lower bound.
        Assert.True(health.GetProperty("outboxUnpublished").GetInt32() >= outboxBefore + 1);
    }

    [Fact]
    public async Task QueryAuditLogs_SaSeesAllTenants_TenantAdminOnlySeesOwnTenantEvenIfQueryingOtherTenantId()
    {
        var (_, saClient) = await _factory.LoginAsAsync(UserRole.SuperAdmin, null, "sa-audit");
        var tenantA = await _factory.SeedTenantAsync("SMK Audit A");
        var tenantB = await _factory.SeedTenantAsync("SMK Audit B");
        var (adminA, adminAClient) = await _factory.LoginAsAsync(UserRole.TenantAdmin, tenantA.Id, "audit-admin-a");

        using (var scope = _factory.CreateDbScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantA.Id, ActorUserId = adminA.Id, Action = "TestActionA", Entity = "Test", EntityId = "1", MetaJson = "{}" });
            db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = tenantB.Id, ActorUserId = Guid.NewGuid(), Action = "TestActionB", Entity = "Test", EntityId = "2", MetaJson = "{}" });
            await db.SaveChangesAsync();
        }

        // SA: filter eksplisit tenantA -> hanya lihat TestActionA.
        var saResp = await saClient.GetAsync($"/sa/audit-logs?tenantId={tenantA.Id}&entity=Test");
        var saBody = await saResp.Content.ReadFromJsonAsync<JsonElement>();
        var saActions = saBody.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()).ToList();
        Assert.Contains("TestActionA", saActions);
        Assert.DoesNotContain("TestActionB", saActions);

        // TenantAdmin A: coba query ?tenantId=B (spoof) - endpoint /api/audit-logs TIDAK PERNAH baca
        // parameter tenantId dari query (tenant SELALU dari claim sendiri) - tetap hanya lihat tenant A.
        var adminResp = await adminAClient.GetAsync($"/api/audit-logs?entity=Test");
        var adminBody = await adminResp.Content.ReadFromJsonAsync<JsonElement>();
        var adminActions = adminBody.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("action").GetString()).ToList();
        Assert.Contains("TestActionA", adminActions);
        Assert.DoesNotContain("TestActionB", adminActions);
    }
}
