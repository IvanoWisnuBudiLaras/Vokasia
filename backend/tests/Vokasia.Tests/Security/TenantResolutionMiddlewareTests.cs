using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Vokasia.Api.Auth;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.TenantContext;
using OpenIddictClaims = OpenIddict.Abstractions.OpenIddictConstants.Claims;

namespace Vokasia.Tests.Security;

public class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task SuperAdmin_LegacyActingTenantHeader_CannotChangeTenantContext()
    {
        var requestedTenantId = Guid.NewGuid();
        var ambient = new AmbientTenantContext();
        var context = AuthenticatedContext(UserRole.SuperAdmin);
        context.Request.Headers["X-Acting-Tenant"] = requestedTenantId.ToString();
        var middleware = new TenantResolutionMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, ambient);

        Assert.Null(ambient.TenantId);
        Assert.Equal(nameof(UserRole.SuperAdmin), ambient.Role);
    }

    private static DefaultHttpContext AuthenticatedContext(UserRole role)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [
                        new Claim(OpenIddictClaims.Subject, Guid.NewGuid().ToString()),
                        new Claim("role", role.ToString()),
                    ],
                    authenticationType: "Test")),
        };

        return context;
    }
}
