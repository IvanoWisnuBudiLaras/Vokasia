using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Vokasia.Worker.Health;

/// <summary>
/// File marker yang dibaca proses healthcheck Compose. Marker hanya dianggap siap jika laporan
/// dependency terakhir sehat dan timestamp-nya masih segar.
/// </summary>
public sealed class WorkerReadinessMarker(
    IConfiguration configuration,
    TimeProvider timeProvider)
{
    public static readonly TimeSpan MaxMarkerAge = TimeSpan.FromSeconds(35);

    public static string DefaultFilePath =>
        Path.Combine(Path.GetTempPath(), "vokasia-worker-ready");

    public string FilePath { get; } =
        configuration["Worker:ReadinessFile"] ?? DefaultFilePath;

    public async Task MarkHealthyAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{FilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
                cancellationToken);
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public bool IsFresh() =>
        IsFresh(FilePath, timeProvider.GetUtcNow(), MaxMarkerAge);

    public void Clear()
    {
        if (File.Exists(FilePath))
        {
            File.Delete(FilePath);
        }
    }

    public static bool IsFresh(
        string filePath,
        DateTimeOffset now,
        TimeSpan maxAge)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            var rawTimestamp = File.ReadAllText(filePath);
            if (!DateTimeOffset.TryParseExact(
                    rawTimestamp,
                    "O",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var publishedAt))
            {
                return false;
            }

            var age = now - publishedAt;
            return age >= TimeSpan.Zero && age <= maxAge;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>
/// Menerjemahkan aggregate health report menjadi readiness marker yang dapat dibaca tanpa
/// menambahkan HTTP server atau executable lain ke image worker.
/// </summary>
public sealed class WorkerReadinessPublisher(
    WorkerReadinessMarker marker,
    ILogger<WorkerReadinessPublisher> logger) : IHealthCheckPublisher
{
    public async Task PublishAsync(
        HealthReport report,
        CancellationToken cancellationToken)
    {
        if (report.Status == HealthStatus.Healthy)
        {
            await marker.MarkHealthyAsync(cancellationToken);
            return;
        }

        marker.Clear();
        var failedDependencies = report.Entries
            .Where(entry => entry.Value.Status != HealthStatus.Healthy)
            .Select(entry => $"{entry.Key}:{entry.Value.Status}");
        logger.LogWarning(
            "Worker belum ready. Dependency gagal: {FailedDependencies}",
            string.Join(", ", failedDependencies));
    }
}
