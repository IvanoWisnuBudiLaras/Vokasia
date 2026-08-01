using System.Security.Cryptography;

namespace Vokasia.Api.Middleware;

/// <summary>
/// Adds baseline browser security headers to every response. API responses keep the restrictive
/// <c>default-src 'none'</c> policy. The standalone account-login HTML receives narrowly scoped
/// style and script exceptions through one cryptographically random, per-request CSP nonce.
/// HSTS remains configured by <c>UseHsts()</c> for non-Development environments.
/// </summary>
public class SecurityHeadersMiddleware
{
    private const string LoginPath = "/account/login";
    private const string LogoutPath = "/account/logout";
    private const string DefaultCsp = "default-src 'none'; frame-ancestors 'none'";
    private static readonly object CspNonceItemKey = new();
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _isDevelopment = env.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var csp = DefaultCsp;
        if (IsHtmlPage(context.Request.Path))
        {
            // 18 byte = 144 bit entropy and encodes without Base64 padding.
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
            context.Items[CspNonceItemKey] = nonce;

            var connectSrc = _isDevelopment
                ? "connect-src 'self' http://localhost:3000 http://localhost:5000"
                : "connect-src 'self'";

            var formAction = _isDevelopment
                ? "form-action 'self' http://localhost:3000 http://localhost:5000"
                : "form-action 'self'";

            csp =
                $"default-src 'none'; " +
                $"style-src 'nonce-{nonce}'; " +
                $"script-src 'nonce-{nonce}'; " +
                $"img-src 'self'; " +
                $"{connectSrc}; " +
                $"{formAction}; " +
                "base-uri 'none'; frame-ancestors 'none'";
        }

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Content-Security-Policy"] = csp;
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=(), browsing-topics=()";
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private static bool IsHtmlPage(PathString path)
    {
        return MatchesPath(path, LoginPath) || MatchesPath(path, LogoutPath);
    }

    private static bool MatchesPath(PathString path, string target)
    {
        var value = path.Value;
        return string.Equals(value, target, StringComparison.OrdinalIgnoreCase) ||
               value is not null &&
               value.Length == target.Length + 1 &&
               value[^1] == '/' &&
               value.StartsWith(target, StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetCspNonce(HttpContext context)
    {
        if (context.Items.TryGetValue(CspNonceItemKey, out var value) &&
            value is string nonce)
        {
            return nonce;
        }

        throw new InvalidOperationException(
            "Nonce CSP untuk halaman login tidak tersedia. Pastikan " +
            "SecurityHeadersMiddleware berjalan sebelum endpoint.");
    }
}

