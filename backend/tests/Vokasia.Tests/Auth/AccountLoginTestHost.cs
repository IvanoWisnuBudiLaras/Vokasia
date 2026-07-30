using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vokasia.Api.Auth;
using Vokasia.Api.Middleware;
using Vokasia.Api.RateLimiting;
using Vokasia.Domain.Common;
using Vokasia.Infrastructure.Identity;

namespace Vokasia.Tests.Auth;

/// <summary>
/// Minimal host for the raw account-login surface. It exercises the real endpoints,
/// Identity password hashing, cookies, and rate-limiting without booting unrelated
/// OpenIddict/database infrastructure.
/// </summary>
public sealed class AccountLoginTestHost : IDisposable
{
    private readonly WebApplication _app;

    // Keep the fixture constructor parameterless: xUnit resolves public constructor parameters
    // as fixture dependencies, so a configurable limit belongs behind an explicit factory.
    public AccountLoginTestHost() : this(1000, true)
    {
    }

    public static AccountLoginTestHost Create(int loginAttemptsPerIp) =>
        new(loginAttemptsPerIp, true);

    private AccountLoginTestHost(int loginAttemptsPerIp, bool _)
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
            });
        builder.WebHost.UseTestServer();
        builder.Configuration["RateLimiting:LoginAttemptsPerIp"] = loginAttemptsPerIp.ToString();
        builder.Services.AddRouting();
        builder.Services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
            });
        builder.Services.AddSingleton<IUserStore<AppUser>, InMemoryUserStore>();
        builder.Services.AddSingleton<IDataProtectionProvider>(
            new EphemeralDataProtectionProvider());
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
        builder.Services.AddAuthorization();
        builder.Services.AddVokasiaRateLimiting(builder.Configuration);
        builder.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "vok_antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        _app = builder.Build();
        _app.UseMiddleware<SecurityHeadersMiddleware>();
        _app.UseRouting();
        _app.Use(async (context, next) =>
        {
            if (context.Request.HasFormContentType)
            {
                await context.Request.ReadFormAsync();
            }

            await next(context);
        });
        _app.UseRateLimiter();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapAccountEndpoints();
        _app.MapGet("/header-probe", () => Results.Text("ok"));
        _app.StartAsync().GetAwaiter().GetResult();
    }

    public HttpClient CreateClient() => _app.GetTestClient();

    public async Task<AppUser> SeedUserAsync(
        string emailLocalPart,
        UserRole role,
        bool isActive = true)
    {
        using var scope = _app.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var email = $"{emailLocalPart}-{Guid.NewGuid():N}@vokasia.test";
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = "Test " + emailLocalPart,
            Role = role,
            TenantId = Guid.NewGuid(),
            IsActive = isActive,
        };

        var created = await userManager.CreateAsync(user, AuthTestHelpers.Password);
        Assert.True(
            created.Succeeded,
            string.Join(", ", created.Errors.Select(error => error.Description)));
        return user;
    }

    public void Dispose() =>
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();

    private sealed class InMemoryUserStore :
        IUserStore<AppUser>,
        IUserEmailStore<AppUser>,
        IUserPasswordStore<AppUser>
    {
        private readonly ConcurrentDictionary<Guid, AppUser> _users = new();

        public Task<IdentityResult> CreateAsync(
            AppUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _users[user.Id] = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(
            AppUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _users.TryRemove(user.Id, out _);
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<AppUser?> FindByEmailAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                _users.Values.SingleOrDefault(
                    user => user.NormalizedEmail == normalizedEmail));
        }

        public Task<AppUser?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Guid.TryParse(userId, out var id) &&
                _users.TryGetValue(id, out var user)
                    ? user
                    : null);
        }

        public Task<AppUser?> FindByNameAsync(
            string normalizedUserName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                _users.Values.SingleOrDefault(
                    user => user.NormalizedUserName == normalizedUserName));
        }

        public Task<string?> GetEmailAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.Email);

        public Task<bool> GetEmailConfirmedAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.EmailConfirmed);

        public Task<string?> GetNormalizedEmailAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedEmail);

        public Task<string?> GetNormalizedUserNameAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName);

        public Task<string?> GetPasswordHashAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.PasswordHash);

        public Task<string> GetUserIdAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.UserName);

        public Task<bool> HasPasswordAsync(
            AppUser user,
            CancellationToken cancellationToken) =>
            Task.FromResult(user.PasswordHash is not null);

        public Task SetEmailAsync(
            AppUser user,
            string? email,
            CancellationToken cancellationToken)
        {
            user.Email = email;
            return Task.CompletedTask;
        }

        public Task SetEmailConfirmedAsync(
            AppUser user,
            bool confirmed,
            CancellationToken cancellationToken)
        {
            user.EmailConfirmed = confirmed;
            return Task.CompletedTask;
        }

        public Task SetNormalizedEmailAsync(
            AppUser user,
            string? normalizedEmail,
            CancellationToken cancellationToken)
        {
            user.NormalizedEmail = normalizedEmail;
            return Task.CompletedTask;
        }

        public Task SetNormalizedUserNameAsync(
            AppUser user,
            string? normalizedName,
            CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(
            AppUser user,
            string? passwordHash,
            CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(
            AppUser user,
            string? userName,
            CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> UpdateAsync(
            AppUser user,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _users[user.Id] = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public void Dispose()
        {
        }
    }
}
