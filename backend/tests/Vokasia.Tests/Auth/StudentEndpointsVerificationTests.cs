using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Vokasia.Api.Endpoints;
using Vokasia.Domain.Common;

namespace Vokasia.Tests.Auth;

[Collection("IntegrationTests")]
public class StudentEndpointsVerificationTests : IClassFixture<Fixtures.VokasiaIntegrationFactory>
{
    private readonly HttpClient _client;

    public StudentEndpointsVerificationTests(Fixtures.VokasiaIntegrationFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    private async Task AuthenticateAsStudentAsync(string email = "siswa1@smkcontoh.local", string password = "DevPass123!")
    {
        var getLogin = await _client.GetAsync("/account/login");
        var html = await getLogin.Content.ReadAsStringAsync();
        
        var nameMatch = Regex.Match(html, @"name=""(?<name>[^""]*RequestVerificationToken[^""]*)""");
        var valMatch = Regex.Match(html, @"name=""[^""]*RequestVerificationToken[^""]*""\s+value=""(?<val>[^""]+)""");
        
        var form = new Dictionary<string, string>
        {
            ["email"] = email,
            ["password"] = password,
            ["returnUrl"] = "/account/continue",
            [nameMatch.Groups["name"].Value] = valMatch.Groups["val"].Value
        };

        var postRes = await _client.PostAsync("/account/login", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.SeeOther, postRes.StatusCode);
    }

    [Fact]
    public async Task GetJournals_ReturnsOk_ForStudent()
    {
        // 1. Login sebagai Siswa
        await AuthenticateAsStudentAsync();

        // 2. Request GET /api/journals?pageSize=200
        var response = await _client.GetAsync("/api/journals?pageSize=200");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 OK but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetJournals_WithFilterStatus_ReturnsOk()
    {
        // 1. Login sebagai Siswa
        await AuthenticateAsStudentAsync();

        // 2. Request GET /api/journals?pageSize=200&status=1 (Approved)
        var response = await _client.GetAsync("/api/journals?pageSize=200&status=1");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 OK for status=1 but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetStudentHome_ReturnsOk()
    {
        // 1. Login sebagai Siswa
        await AuthenticateAsStudentAsync();

        // 2. Request GET /api/students/me/home
        var response = await _client.GetAsync("/api/students/me/home");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 OK for me/home but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetLearningRecords_ReturnsOk()
    {
        // 1. Login sebagai Siswa
        await AuthenticateAsStudentAsync();

        // 2. Request GET /api/students/me/learning-records
        var response = await _client.GetAsync("/api/students/me/learning-records");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 OK for learning-records but got {response.StatusCode}");
    }

    [Fact]
    public async Task GetPortfolio_ReturnsOk()
    {
        // 1. Login sebagai Siswa
        await AuthenticateAsStudentAsync();

        // 2. Request GET /api/portfolio
        var response = await _client.GetAsync("/api/portfolio");
        
        // Assert
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 OK for portfolio but got {response.StatusCode}");
    }
}
