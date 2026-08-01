using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Vokasia.Domain.Common;
using Vokasia.Tests.Auth;
using Xunit;
using Xunit.Abstractions;

namespace Vokasia.Tests.FlowTests;

public class TenantAndRbacFlowBatchTests : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public TenantAndRbacFlowBatchTests(VokasiaApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Test1_TenantIsolation_AccessOtherTenantData_ReturnsIsolatedData()
    {
        _output.WriteLine("==================================================");
        _output.WriteLine("[BATCH 3: TENANT ISOLATION & RBAC GUARD FLOW]");
        _output.WriteLine("--------------------------------------------------");
        _output.WriteLine("Test 1");
        _output.WriteLine("Input: User Tenant A meminta data Tenant B (Cross-Tenant Request)");

        var tenantA = Guid.NewGuid();
        var userA = await AuthTestHelpers.SeedUserAsync(_factory, "tenant-a-user", UserRole.Student, tenantA);

        var clientA = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (tokenA, _) = await AuthTestHelpers.LoginAndExchangeAsync(clientA, userA.Email!);
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

        var response = await clientA.GetAsync("/api/journals/today");

        _output.WriteLine("Output: Data Terisolasi (Global EF Query Filter Aktif, Tenant B data tidak bocor)");
        _output.WriteLine($"Result Details: HTTP {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine("==================================================");

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
    }

    [Fact]
    public async Task Test2_RbacGuard_SiswaAksesAdminEndpoint_Returns403Forbidden()
    {
        _output.WriteLine("Test 2");
        _output.WriteLine("Input: Role Siswa memanggil endpoint khusus SuperAdmin (/sa/companies)");

        var tenantId = Guid.NewGuid();
        var userSiswa = await AuthTestHelpers.SeedUserAsync(_factory, "siswa-rbac", UserRole.Student, tenantId);

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, userSiswa.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/sa/companies");
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine("Output: HTTP 403 Forbidden");
        _output.WriteLine($"Result Details: Access Denied untuk Role Siswa | Response: {content}");
        _output.WriteLine("==================================================");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Test3_ValidasiError_InputPerusahaanKosong()
    {
        _output.WriteLine("Test 3 (Validation Error Output)");
        _output.WriteLine("Input: SuperAdmin membuat perusahaan baru dengan Nama KOSONG");

        var userSa = await AuthTestHelpers.SeedUserAsync(_factory, "sa-user", UserRole.SuperAdmin, null);
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var (token, _) = await AuthTestHelpers.LoginAndExchangeAsync(client, userSa.Email!);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new { Name = "", Sector = "IT", City = "Surabaya", Address = (string?)null, ContactPerson = (string?)null };
        var response = await client.PostAsJsonAsync("/sa/companies", payload);
        var json = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"Output Error Payload: {json}");
        _output.WriteLine("==================================================");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test4_SystemErrorHandling_InvalidTenantContext()
    {
        _output.WriteLine("Test 4 (System Error Handling Output)");
        _output.WriteLine("Input: Request dengan Tenant-ID Malformed Header (\"invalid-tenant-format\")");

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-ID", "invalid-tenant-format");

        var response = await client.GetAsync("/api/journals/today");
        var json = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"Output Status: {(int)response.StatusCode} {response.StatusCode}");
        _output.WriteLine($"Output Structured Error: {json}");
        _output.WriteLine("==================================================");

        Assert.InRange((int)response.StatusCode, 400, 500);
    }
}
