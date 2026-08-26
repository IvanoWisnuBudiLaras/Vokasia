using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Vokasia.Tests.Auth;

[Collection("IntegrationTests")]
public class LoginTests : IClassFixture<Fixtures.VokasiaIntegrationFactory>
{
    private readonly HttpClient _client;

    public LoginTests(Fixtures.VokasiaIntegrationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true // Otomatis mengelola cookie session & antiforgery
        });
    }

    private async Task<(string FormFieldName, string RequestToken)> ExtractAntiforgeryTokensAsync()
    {
        var response = await _client.GetAsync("/account/login");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        
        // Match: <input type="hidden" name="__RequestVerificationToken" value="..." />
        var nameMatch = Regex.Match(html, @"name=""(?<name>[^""]*RequestVerificationToken[^""]*)""");
        var valMatch = Regex.Match(html, @"name=""[^""]*RequestVerificationToken[^""]*""\s+value=""(?<val>[^""]+)""");
        
        Assert.True(nameMatch.Success && valMatch.Success, "Input hidden antiforgery token harus ada di form login HTML");

        return (nameMatch.Groups["name"].Value, valMatch.Groups["val"].Value);
    }

    [Fact]
    public async Task GetLogin_ReturnsOk_WithHtmlForm()
    {
        // Act
        var response = await _client.GetAsync("/account/login");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form", html);
        Assert.Contains("action=\"/account/login\"", html);
        Assert.Contains("type=\"email\"", html);
        Assert.Contains("type=\"password\"", html);
    }

    [Theory]
    [InlineData("superadmin@vokasia.local", "DevPass123!")]
    [InlineData("admin@smkcontoh.local", "DevPass123!")]
    [InlineData("guru@smkcontoh.local", "DevPass123!")]
    [InlineData("siswa1@smkcontoh.local", "DevPass123!")]
    public async Task PostLogin_ValidCredentials_LogsInSuccessfully_AndRedirects(string email, string password)
    {
        // Arrange
        var (fieldName, token) = await ExtractAntiforgeryTokensAsync();

        var form = new Dictionary<string, string>
        {
            [fieldName] = token,
            ["email"] = email,
            ["password"] = password,
            ["returnUrl"] = "/account/continue"
        };

        // Act
        var response = await _client.PostAsync("/account/login", new FormUrlEncodedContent(form));

        // Assert: Harus redirect 303 See Other ke returnUrl yang diminta
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Equal("/account/continue", location);
    }

    [Fact]
    public async Task PostLogin_InvalidPassword_RedirectsBackToLogin_WithErrorParam()
    {
        // Arrange
        var (fieldName, token) = await ExtractAntiforgeryTokensAsync();

        var form = new Dictionary<string, string>
        {
            [fieldName] = token,
            ["email"] = "admin@smkcontoh.local",
            ["password"] = "KataSandiSalah123!",
            ["returnUrl"] = "/account/continue"
        };

        // Act
        var response = await _client.PostAsync("/account/login", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("/account/login", location);
        Assert.Contains("error=", location);
    }

    [Fact]
    public async Task PostLogin_UnknownUser_RedirectsBackToLogin_WithErrorParam()
    {
        // Arrange
        var (fieldName, token) = await ExtractAntiforgeryTokensAsync();

        var form = new Dictionary<string, string>
        {
            [fieldName] = token,
            ["email"] = "tidakada@domain.invalid",
            ["password"] = "DevPass123!",
            ["returnUrl"] = "/account/continue"
        };

        // Act
        var response = await _client.PostAsync("/account/login", new FormUrlEncodedContent(form));

        // Assert
        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.StartsWith("/account/login", location);
        Assert.Contains("error=", location);
    }
}
