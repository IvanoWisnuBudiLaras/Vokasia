using Minio;
using Minio.DataModel.Args;

namespace Vokasia.Api.Storage;

public interface IBrowserObjectStorageSigner
{
    Task<string> PresignedPutObjectAsync(PresignedPutObjectArgs args);
    Task<string> PresignedGetObjectAsync(PresignedGetObjectArgs args);
}

public sealed class BrowserObjectStorageSigner : IBrowserObjectStorageSigner
{
    private readonly IMinioClient _client;

    public BrowserObjectStorageSigner(IConfiguration configuration)
    {
        var configuredEndpoint = configuration["Minio:PublicEndpoint"]
            ?? configuration["MINIO_PUBLIC_URL"]
            ?? configuration["Minio:Endpoint"]
            ?? "localhost:9000";
        var endpoint = configuredEndpoint.Contains("://", StringComparison.Ordinal)
            ? new Uri(configuredEndpoint)
            : new Uri($"http://{configuredEndpoint}");

        _client = new MinioClient()
            .WithEndpoint(endpoint.Host, endpoint.Port)
            .WithCredentials(
                configuration["Minio:AccessKey"] ?? "vokasia",
                configuration["Minio:SecretKey"] ?? "vokasia_dev")
            .WithSSL(endpoint.Scheme == Uri.UriSchemeHttps)
            .Build();
    }

    public Task<string> PresignedPutObjectAsync(PresignedPutObjectArgs args) =>
        _client.PresignedPutObjectAsync(args);

    public Task<string> PresignedGetObjectAsync(PresignedGetObjectArgs args) =>
        _client.PresignedGetObjectAsync(args);
}
