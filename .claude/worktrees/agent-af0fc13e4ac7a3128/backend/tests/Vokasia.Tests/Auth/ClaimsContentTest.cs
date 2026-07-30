using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;

namespace Vokasia.Tests.Auth;

/// <summary>
/// AC VOK-H1-E3: token (via VokasiaClaimsFactory, sumber tunggal claims) memuat sub/tenant_id/role
/// sesuai user — RBAC & tenant filter H2-E3 bergantung pada ini.
/// </summary>
public class ClaimsContentTest : IClassFixture<VokasiaApiFactory>
{
    private readonly VokasiaApiFactory _factory;

    public ClaimsContentTest(VokasiaApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GenerateClaimsAsync_TenantUser_IncludesSubTenantIdRoleName()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var claimsFactory = scope.ServiceProvider.GetRequiredService<VokasiaClaimsFactory>();

        var tenantId = Guid.NewGuid();
        var user = new AppUser
        {
            UserName = $"guru-{Guid.NewGuid():N}@vokasia.test",
            Email = $"guru-{Guid.NewGuid():N}@vokasia.test",
            FullName = "Guru Uji Coba",
            Role = UserRole.Teacher,
            TenantId = tenantId,
        };

        var created = await userManager.CreateAsync(user, "Password123!");
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

        var identity = await claimsFactory.GenerateClaimsAsync(user);

        Assert.Equal(user.Id.ToString(), identity.FindFirst(OpenIddictConstants.Claims.Subject)?.Value);
        Assert.Equal(tenantId.ToString(), identity.FindFirst("tenant_id")?.Value);
        Assert.Equal(nameof(UserRole.Teacher), identity.FindFirst("role")?.Value);
        Assert.Equal("Guru Uji Coba", identity.FindFirst("name")?.Value);
    }

    [Fact]
    public async Task GenerateClaimsAsync_SuperAdminHasNoTenant_OmitsTenantIdClaim()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var claimsFactory = scope.ServiceProvider.GetRequiredService<VokasiaClaimsFactory>();

        var user = new AppUser
        {
            UserName = $"sa-{Guid.NewGuid():N}@vokasia.test",
            Email = $"sa-{Guid.NewGuid():N}@vokasia.test",
            FullName = "Super Admin Uji",
            Role = UserRole.SuperAdmin,
            TenantId = null,
        };

        var created = await userManager.CreateAsync(user, "Password123!");
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

        var identity = await claimsFactory.GenerateClaimsAsync(user);

        Assert.Equal(nameof(UserRole.SuperAdmin), identity.FindFirst("role")?.Value);
        Assert.Null(identity.FindFirst("tenant_id"));
    }
}
