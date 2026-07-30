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
    private const string DefaultCsp = "default-src 'none'; frame-ancestors 'none'";
    private static readonly object CspNonceItemKey = new();
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var csp = DefaultCsp;
        if (IsLoginPath(context.Request.Path))
        {
            // 18 byte = 144 bit entropy and encodes without Base64 padding.
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
            context.Items[CspNonceItemKey] = nonce;
            csp =
                $"default-src 'none'; style-src 'nonce-{nonce}'; script-src 'nonce-{nonce}'; " +
                "form-action 'self'; " +
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

    private static bool IsLoginPath(PathString path)
    {
        var value = path.Value;
        return string.Equals(value, LoginPath, StringComparison.OrdinalIgnoreCase) ||
               value is not null &&
               value.Length == LoginPath.Length + 1 &&
               value[^1] == '/' &&
               value.StartsWith(LoginPath, StringComparison.OrdinalIgnoreCase);
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
