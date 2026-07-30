using System.Runtime.CompilerServices;

namespace Vokasia.Tests.Infrastructure;

internal static class DockerEndpointNormalizer
{
    private const string WindowsPipePrefix = "npipe:////./pipe/";
    private const string TestcontainersPipePrefix = "npipe://./pipe/";

    public static string? Normalize(string? endpoint, bool isWindows)
    {
        if (!isWindows || string.IsNullOrWhiteSpace(endpoint) ||
            !endpoint.StartsWith(WindowsPipePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return TestcontainersPipePrefix + endpoint[WindowsPipePrefix.Length..];
    }

    [ModuleInitializer]
    internal static void NormalizeProcessDockerHost()
    {
        var current = Environment.GetEnvironmentVariable("DOCKER_HOST");
        var normalized = Normalize(current, OperatingSystem.IsWindows());
        if (!string.Equals(current, normalized, StringComparison.Ordinal))
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", normalized);
        }
    }
}
