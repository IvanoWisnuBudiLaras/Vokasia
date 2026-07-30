using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Minio;
using Minio.DataModel.Args;
using StackExchange.Redis;
using Vokasia.Infrastructure.Persistence;

namespace Vokasia.Worker.Health;

internal sealed class WorkerPostgresHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<VokasiaDbContext>();
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Gagal terhubung ke Postgres.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Pemeriksaan Postgres gagal.", ex);
        }
    }
}

internal sealed class WorkerRedisHealthCheck(
    IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await multiplexer.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Pemeriksaan Redis gagal.", ex);
        }
    }
}

internal sealed class WorkerMinioHealthCheck(
    IMinioClient minio,
    IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var bucket = configuration["Minio:Bucket"] ?? "vokasia-journal";
            var exists = await minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucket),
                cancellationToken);
            return exists
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Bucket MinIO worker belum tersedia.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Pemeriksaan MinIO gagal.", ex);
        }
    }
}
