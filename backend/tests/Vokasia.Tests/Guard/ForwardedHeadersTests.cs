using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Vokasia.Api.Middleware;

namespace Vokasia.Tests.Guard;

public sealed class ForwardedHeadersTests
{
    [Fact]
    public void Configure_UsesForwardedForAndProto_OnlyForConfiguredProxy()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ForwardedHeaders:KnownProxies:0"] = "10.0.0.2",
                ["ForwardedHeaders:KnownIPNetworks:0"] = "10.20.0.0/16",
                ["ForwardedHeaders:AllowedHosts:0"] = "app.example.test",
            })
            .Build();
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersSetup.Configure(options, configuration);

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(IPAddress.Parse("10.0.0.2"), options.KnownProxies);
        Assert.Contains(options.KnownIPNetworks, network => network.Contains(IPAddress.Parse("10.20.1.10")));
        Assert.Contains("app.example.test", options.AllowedHosts);
    }
}
