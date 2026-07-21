using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Vokasia.Tests.Auth;

/// <summary>AC VOK-H1-E3: access token 15 menit, refresh sliding 14 hari (NFR-SEC-01).</summary>
public class TokenLifetimeTest : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;

    public TokenLifetimeTest(VokasiaApiFactory factory) => _factory = factory;

    [Fact]
    public void AccessTokenLifetime_Is15Minutes()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>();

        Assert.Equal(TimeSpan.FromMinutes(15), options.CurrentValue.AccessTokenLifetime);
    }

    [Fact]
    public void RefreshTokenLifetime_Is14Days()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>();

        Assert.Equal(TimeSpan.FromDays(14), options.CurrentValue.RefreshTokenLifetime);
    }
}
