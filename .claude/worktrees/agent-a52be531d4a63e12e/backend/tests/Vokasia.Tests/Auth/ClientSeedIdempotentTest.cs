using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Vokasia.Api.Auth;

namespace Vokasia.Tests.Auth;

/// <summary>AC VOK-H1-E3: SeedOAuthClientsAsync aman dipanggil berkali-kali (startup app + test ini).</summary>
public class ClientSeedIdempotentTest : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;

    public ClientSeedIdempotentTest(VokasiaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task SeedOAuthClientsAsync_CalledRepeatedly_NeverDuplicatesClient()
    {
        // Program.cs sudah memanggil seed 1x saat factory boot. Panggil 2x lagi di sini.
        await OpenIddictSetup.SeedOAuthClientsAsync(_factory.Services);
        await OpenIddictSetup.SeedOAuthClientsAsync(_factory.Services);

        using var scope = _factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        var count = 0;
        await foreach (var _ in manager.ListAsync())
        {
            count++;
        }

        Assert.Equal(1, count);

        var client = await manager.FindByClientIdAsync(OpenIddictSetup.BffClientId);
        Assert.NotNull(client);
    }
}
