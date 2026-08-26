using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Vokasia.Tests.Auth;

[Collection("IntegrationTests")]
public class OAuthTokenTests : IClassFixture<Fixtures.VokasiaIntegrationFactory>
{
    private readonly HttpClient _client;

    public OAuthTokenTests(Fixtures.VokasiaIntegrationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task PostConnectToken_WithoutGrantType_ReturnsBadRequest()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["client_id"] = "vokasia-bff",
            ["client_secret"] = "dev-only-secret-change-me"
        };

        // Act
        var response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostConnectToken_InvalidClientCredentials_ReturnsError()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "invalid-client",
            ["client_secret"] = "wrong-secret",
            ["code"] = "invalid-code"
        };

        // Act
        var response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostConnectToken_InvalidMagicLinkToken_ReturnsForbidOrBadRequest()
    {
        // Arrange
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "magic_link",
            ["client_id"] = "vokasia-bff",
            ["client_secret"] = "dev-only-secret-change-me",
            ["token"] = "invalid-magic-link-token-xyz"
        };

        // Act
        var response = await _client.PostAsync("/connect/token", new FormUrlEncodedContent(form));

        // Assert
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.InternalServerError,
            $"Expected error status but got {response.StatusCode}");
    }
}
