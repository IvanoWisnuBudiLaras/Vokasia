using Microsoft.Extensions.Configuration;

namespace Vokasia.Infrastructure.Configuration;

/// <summary>Resolves the one application origin used in public links and credential artifacts.</summary>
public static class PublicAppOrigin
{
    public const string ConfigurationKey = "PublicAppBaseUrl";

    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration[ConfigurationKey]
            ?? configuration["Frontend:PublicUrl"]
            ?? configuration["NEXT_PUBLIC_APP_URL"];
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
        var isProduction = string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(configured))
        {
            if (isProduction)
            {
                throw new InvalidOperationException($"{ConfigurationKey} wajib dikonfigurasi di Production.");
            }

            return "http://localhost:3000";
        }

        if (!Uri.TryCreate(configured.TrimEnd('/'), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host) ||
            (isProduction && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"{ConfigurationKey} harus berupa URL HTTP(S) absolut.");
        }

        return uri.ToString().TrimEnd('/');
    }
}
