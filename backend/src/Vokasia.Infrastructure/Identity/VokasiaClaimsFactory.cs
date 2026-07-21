using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Vokasia.Infrastructure.Identity;

/// <summary>
/// Sumber tunggal isi claims token (sub, tenant_id, role, name). RBAC (H2-E3) dan tenant
/// query filter (VokasiaDbContext) keduanya bergantung pada claims yang dihasilkan di sini —
/// jangan taruh logika claims lain di tempat kedua.
/// </summary>
public class VokasiaClaimsFactory : UserClaimsPrincipalFactory<AppUser, AppRole>
{
    public VokasiaClaimsFactory(
        UserManager<AppUser> userManager,
        RoleManager<AppRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    public new async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var principal = await CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        identity.AddClaim(new Claim(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim("role", user.Role.ToString()));
        identity.AddClaim(new Claim("name", user.FullName));
        if (user.TenantId.HasValue)
        {
            identity.AddClaim(new Claim("tenant_id", user.TenantId.Value.ToString()));
        }

        return identity;
    }
}
