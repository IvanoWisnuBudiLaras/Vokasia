namespace Vokasia.Tests.Infrastructure;

public sealed class DockerEndpointNormalizerTests
{
    [Fact]
    public void NormalizesDockerDesktopWindowsPipeForTestcontainers()
    {
        Assert.Equal(
            "npipe://./pipe/dockerDesktopLinuxEngine",
            DockerEndpointNormalizer.Normalize("npipe:////./pipe/dockerDesktopLinuxEngine", isWindows: true));
    }

    [Fact]
    public void LeavesUnixAndAlreadyNormalizedEndpointsUntouched()
    {
        Assert.Equal("unix:///var/run/docker.sock", DockerEndpointNormalizer.Normalize("unix:///var/run/docker.sock", isWindows: true));
        Assert.Equal("npipe://./pipe/docker_engine", DockerEndpointNormalizer.Normalize("npipe://./pipe/docker_engine", isWindows: true));
        Assert.Equal("npipe:////./pipe/dockerDesktopLinuxEngine", DockerEndpointNormalizer.Normalize("npipe:////./pipe/dockerDesktopLinuxEngine", isWindows: false));
    }
}
