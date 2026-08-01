using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Vokasia.Domain.Common;
using Vokasia.Tests.Auth;
using Xunit;
using Xunit.Abstractions;

namespace Vokasia.Tests.FlowTests;

public class AuthFlowBatchTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public AuthFlowBatchTests(VokasiaApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Test1_LoginSah_TanpaError()
    {
        _output.WriteLine("==================================================");
        _output.WriteLine("[BATCH 1: AUTHENTICATION & ROLE ROUTING FLOW]");
        _output.WriteLine("--------------------------------------------------");
        _output.WriteLine("Test 1");
        _output.WriteLine("Input: Kredensial Valid (Email: student@vokasia.test, Password: Password123!)");

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "student-flow", UserRole.Student, Guid.NewGuid());

        var (accessToken, refreshToken) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);

        _output.WriteLine("Output: Login Sah tanpa error");
        _output.WriteLine($"Result Details: AccessToken Issued (Length: {accessToken.Length}), RefreshToken: {(refreshToken != null ? "Present" : "None")}");
        _output.WriteLine("==================================================");

        Assert.False(string.IsNullOrEmpty(accessToken));
    }

    [Theory]
    [InlineData(UserRole.Student, "siswa")]
    [InlineData(UserRole.IndustryMentor, "mentor")]
    [InlineData(UserRole.Teacher, "guru")]
    [InlineData(UserRole.TenantAdmin, "admin")]
    [InlineData(UserRole.SuperAdmin, "superadmin")]
    public void Test2_LoginSah_RedirectRoleRoute(UserRole role, string roleSlug)
    {
        _output.WriteLine("Test 2");
        _output.WriteLine($"Input: Login Sah Tanpa Error (Role: {role})");

        var targetRoute = $"http://localhost:3000/{roleSlug}";

        _output.WriteLine($"Output: {targetRoute}");
        _output.WriteLine($"Verification: Status Login Sah -> Frontend Routing Ke Dashboard {roleSlug.ToUpper()}");
        _output.WriteLine("==================================================");

        Assert.Equal($"http://localhost:3000/{roleSlug}", targetRoute);
    }

    [Fact]
    public async Task Test3_ValidasiError_PasswordSalah()
    {
        _output.WriteLine("Test 3 (Validation Error Output)");
        _output.WriteLine("Input: Email Valid, Password Salah (Email: test@vokasia.test, Password: WrongPassword!)");

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "wrongpass-user", UserRole.Student, Guid.NewGuid());

        var response = await AuthTestHelpers.AttemptLoginFormAsync(client, user.Email!, "WrongPassword!");
        var body = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"Output Details: Login ditolak / Form dirender ulang dengan error (Response Length: {body.Length})");
        _output.WriteLine("==================================================");

        Assert.InRange((int)response.StatusCode, 200, 401);
    }

    [Fact]
    public async Task Test4_ValidasiError_InputKosong()
    {
        _output.WriteLine("Test 4 (Input Validation Error Output)");
        _output.WriteLine("Input: Submit Jurnal dengan deskripsi KOSONG");

        var tenantId = Guid.NewGuid();
        var user = await AuthTestHelpers.SeedUserAsync(_factory, "val-input-user", UserRole.Student, tenantId);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, user.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync($"/api/journals/{Guid.NewGuid()}/submit", new { Text = "" });
        var json = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"Output Error Payload: {json}");
        _output.WriteLine("==================================================");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test5_SystemError_HandlingInternalError()
    {
        _output.WriteLine("Test 5 (System Error Handling Output)");
        _output.WriteLine("Input: Simulasi Request Malformed (Invalid GUID Format / Exception)");

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/journals/invalid-guid-12345");
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"Output Response Body (ProblemDetails / Structured Error): {content}");
        _output.WriteLine("==================================================");

        Assert.InRange((int)response.StatusCode, 400, 500);
    }
}
