extern alias ApiAssembly;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Vokasia.Tests.Integration;

/// <summary>HTTP-level CORS regression source. Runtime requires the integration fixture/toolchain.</summary>
public sealed class CorsSecurityTests : IClassFixture<VokasiaIntegrationFactory>
{
    private readonly VokasiaIntegrationFactory _factory;
    public CorsSecurityTests(VokasiaIntegrationFactory factory) => _factory = factory;

    [Fact]
    public async Task TrustedFrontendOrigin_PreflightAllowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/periods");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        var response = await _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).SendAsync(request);
        Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK);
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Contains("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
        Assert.Contains("content-type", response.Headers.GetValues("Access-Control-Allow-Headers").Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UntrustedOrigin_NotAllowed()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/periods");
        request.Headers.Add("Origin", "https://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await _factory.CreateClient().SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_DoesNotReturnWildcardForAuthenticatedOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/periods");
        request.Headers.Add("Origin", "http://localhost:3000");
        var response = await _factory.CreateClient().SendAsync(request);
        Assert.NotEqual("*", response.Headers.GetValues("Access-Control-Allow-Origin").SingleOrDefault());
    }

    [Fact]
    public async Task TrustedOrigin_ActualRequest_HasAllowOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ping");
        request.Headers.Add("Origin", "http://localhost:3000");
        var response = await _factory.CreateClient().SendAsync(request);
        Assert.Equal("http://localhost:3000", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
