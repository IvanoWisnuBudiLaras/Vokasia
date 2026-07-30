using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Vokasia.Api.Middleware;

/// <summary>
/// Configures forwarded headers without trusting arbitrary client-supplied values. A reverse
/// proxy must be listed explicitly via <c>ForwardedHeaders:KnownProxies</c> or
/// <c>ForwardedHeaders:KnownIPNetworks</c>; the framework's loopback default remains in place
/// when no proxy is configured.
/// </summary>
public static class ForwardedHeadersSetup
{
    public static void Configure(ForwardedHeadersOptions options, IConfiguration configuration)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;

        AddValues(configuration.GetSection("ForwardedHeaders:KnownProxies"), value =>
        {
            if (IPAddress.TryParse(value, out var address))
            {
                options.KnownProxies.Add(address);
            }
        });

        AddValues(configuration.GetSection("ForwardedHeaders:KnownIPNetworks"), value =>
        {
            if (System.Net.IPNetwork.TryParse(value, out var network))
            {
                options.KnownIPNetworks.Add(network);
            }
        });

        AddValues(configuration.GetSection("ForwardedHeaders:AllowedHosts"), value =>
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                options.AllowedHosts.Add(value);
            }
        });
    }

    private static void AddValues(IConfigurationSection section, Action<string> add)
    {
        foreach (var child in section.GetChildren())
        {
            var value = child.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                add(value.Trim());
            }
        }

        // Also support a comma-separated env var (`KnownProxies=10.0.0.2,10.0.0.3`).
        foreach (var value in (section.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            add(value);
        }
    }
}
