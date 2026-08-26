using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vokasia.Api.Auth;
using Vokasia.Api.Authorization;
using Vokasia.Domain.Common;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Api.Endpoints;

/// <summary>
/// VOK-H6-E1 §4 — Ops (FR-SA-05..07): dashboard KPI platform (W5), panel kesehatan sistem, viewer
/// audit log (SA lihat SEMUA tenant — versi tenant-scoped ada di AuditEndpoints.GetTenantAuditLogs,
/// policy TenantAdmin, endpoint TERPISAH sesuai AC ticket literal).
/// </summary>
public static class SaOpsEndpoints
{
    public static IEndpointRouteBuilder MapSaOpsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/sa").WithTags("SaOps").RequireAuthorization(RbacPolicies.SaOnly);

        group.MapGet("/kpis", GetPlatformKpis);
        group.MapGet("/health", GetSystemHealth);
        group.MapGet("/audit-logs", QueryAuditLogs);

        return app;
    }

    private static async Task<IResult> GetPlatformKpis(VokasiaDbContext db, CancellationToken ct)
    {
        var activeTenants = await db.Tenants.AsNoTracking().CountAsync(t => t.IsActive, ct);

        var activeStudents = await db.Placements.AsNoTracking()
            .Where(p => p.Status == PlacementStatus.Active)
            .Select(p => p.StudentId).Distinct().CountAsync(ct);

        var today = AppTimeZone.TodayJakarta();
        var todaySlots = await db.JournalSlots.AsNoTracking().Where(s => s.Date == today).ToListAsync(ct);
        var journalsToday = todaySlots.Count(s => s.Status == JournalSlotStatus.Filled);
        var journalFillRate = todaySlots.Count == 0 ? 0d : Math.Round(journalsToday * 100d / todaySlots.Count, 1);

        // MRR = Σ plan tenant AKTIF (AC ticket literal) — tenant nonaktif/tanpa plan TIDAK dihitung.
        var mrr = await db.Tenants.AsNoTracking()
            .Where(t => t.IsActive && t.PlanId.HasValue)
            .Join(db.Plans.AsNoTracking(), t => t.PlanId, p => p.Id, (t, p) => p.PriceMonthly)
            .SumAsync(price => (decimal?)price, ct) ?? 0m;

        return Results.Ok(new KpiDto(activeTenants, activeStudents, journalsToday, journalFillRate, mrr));
    }

    /// <summary>
    /// AC/ticket literal: "sumber: RabbitMQ mgmt API + Hangfire storage + outbox count". [GAP dicatat
    /// eksplisit]: QueueDepth/DlqCount (RabbitMQ mgmt HTTP, best-effort raw HttpClient — TANPA paket
    /// NuGet baru) & FailedJobs (query SQL MENTAH thd tabel skema "hangfire" yang DIBUAT Hangfire.
    /// PostgreSql sendiri di Worker — TANPA menambah paket Hangfire.Core/Hangfire.PostgreSql ke
    /// Vokasia.Api, yang akan melanggar AGENTS.md #13 tanpa persetujuan Developer) SEMUANYA best-
    /// effort dibungkus try/catch (pola sama MinIO startup Program.cs) - null kalau tak terjangkau,
    /// BUKAN 500. ApiP95Ms/DiskPct SELALU null (tak ada infra APM/disk-metrics apa pun di repo ini
    /// sampai ticket ini - TIPE NULLABLE di DTO sendiri sengaja mengakomodasi ini, ticket literal
    /// menulis "ApiP95Ms?, DiskPct?").
    /// </summary>
    private static async Task<IResult> GetSystemHealth(VokasiaDbContext db, IConfiguration config, ILogger<Program> logger, CancellationToken ct)
    {
        var outboxUnpublished = await db.OutboxMessages.AsNoTracking().CountAsync(m => m.PublishedAt == null, ct);

        int? queueDepth = null;
        int? dlqCount = null;
        try
        {
            (queueDepth, dlqCount) = await GetRabbitMqQueueStatsAsync(config, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetSystemHealth: gagal ambil statistik RabbitMQ mgmt API - QueueDepth/DlqCount null.");
        }

        int? failedJobs = null;
        try
        {
            failedJobs = await GetHangfireFailedJobCountAsync(db, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetSystemHealth: gagal query tabel Hangfire mentah - FailedJobs null.");
        }

        return Results.Ok(new HealthDto(queueDepth, dlqCount, failedJobs, outboxUnpublished, ApiP95Ms: null, DiskPct: null));
    }

    private static async Task<(int QueueDepth, int DlqCount)> GetRabbitMqQueueStatsAsync(IConfiguration config, CancellationToken ct)
    {
        var host = config["RabbitMq:Host"] ?? "localhost";
        var managementPort = config["RabbitMq:ManagementPort"] ?? "15672";
        var username = config["RabbitMq:Username"] ?? "vokasia";
        var password = config["RabbitMq:Password"] ?? "vokasia_dev";

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{username}:{password}")));

        var resp = await http.GetAsync($"http://{host}:{managementPort}/api/queues", ct);
        resp.EnsureSuccessStatusCode();
        var queues = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        var totalDepth = 0;
        var dlqDepth = 0;
        foreach (var q in queues.EnumerateArray())
        {
            var messages = q.TryGetProperty("messages", out var m) ? m.GetInt32() : 0;
            totalDepth += messages;
            var name = q.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            // Konvensi penamaan MassTransit utk fault queue ("_error") — bukan konsep DLQ generik RabbitMQ.
            if (name.EndsWith("_error", StringComparison.Ordinal))
            {
                dlqDepth += messages;
            }
        }

        return (totalDepth, dlqDepth);
    }

    private static async Task<int> GetHangfireFailedJobCountAsync(VokasiaDbContext db, CancellationToken ct)
    {
        // Skema "hangfire" dibuat Hangfire.PostgreSql (Worker/Program.cs UsePostgreSqlStorage) di
        // DATABASE YANG SAMA - query mentah, BUKAN via paket Hangfire (yang tak direferensi Api).
        var result = await db.Database.SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM hangfire.job WHERE statename = 'Failed'").FirstOrDefaultAsync(ct);
        return result;
    }

    private static async Task<IResult> QueryAuditLogs(
        VokasiaDbContext db, CancellationToken ct,
        [FromQuery] Guid? tenantId = null, [FromQuery] Guid? actorId = null, [FromQuery] string? entity = null,
        [FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = db.AuditLogs.AsNoTracking().AsQueryable();
        if (tenantId.HasValue) query = query.Where(a => a.TenantId == tenantId.Value);
        if (actorId.HasValue) query = query.Where(a => a.ActorUserId == actorId.Value);
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.Entity == entity);
        if (from.HasValue) query = query.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) query = query.Where(a => a.CreatedAt <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AuditDto(a.Id, a.TenantId, a.ActorUserId, a.ActingAsUserId, a.Action, a.Entity, a.EntityId, a.MetaJson, a.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new Paged<AuditDto>(items, page, pageSize, total));
    }
}
