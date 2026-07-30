using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Vokasia.Worker.Health;

namespace Vokasia.Tests.Worker;

public sealed class WorkerReadinessTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"vokasia-worker-readiness-{Guid.NewGuid():N}");
    private readonly ManualTimeProvider _time = new(
        new DateTimeOffset(2026, 7, 29, 8, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Marker_IsReadyOnlyWhileLastHealthyPublicationIsFresh()
    {
        var marker = CreateMarker();

        Assert.False(marker.IsFresh());

        await marker.MarkHealthyAsync(CancellationToken.None);
        Assert.True(marker.IsFresh());

        _time.Advance(WorkerReadinessMarker.MaxMarkerAge + TimeSpan.FromSeconds(1));
        Assert.False(marker.IsFresh());
    }

    [Fact]
    public async Task Publisher_RemovesReadyMarkerWhenAnyDependencyIsUnhealthy()
    {
        var marker = CreateMarker();
        var publisher = new WorkerReadinessPublisher(
            marker,
            NullLogger<WorkerReadinessPublisher>.Instance);

        await publisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None);
        Assert.True(marker.IsFresh());

        await publisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None);
        Assert.False(marker.IsFresh());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private WorkerReadinessMarker CreateMarker()
    {
        var markerPath = Path.Combine(_directory, "ready");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Worker:ReadinessFile"] = markerPath,
            })
            .Build();

        return new WorkerReadinessMarker(configuration, _time);
    }

    private static HealthReport CreateReport(HealthStatus status)
    {
        var entry = new HealthReportEntry(
            status,
            description: null,
            duration: TimeSpan.Zero,
            exception: null,
            data: new Dictionary<string, object>(),
            tags: ["ready"]);

        return new HealthReport(
            new Dictionary<string, HealthReportEntry> { ["dependency"] = entry },
            TimeSpan.Zero);
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration) => _now += duration;
    }
}
