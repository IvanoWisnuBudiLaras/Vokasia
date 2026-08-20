using Microsoft.Extensions.Configuration;
using Minio.DataModel.Args;
using Vokasia.Api.Storage;

namespace Vokasia.Tests.Guard;

public class BrowserObjectStorageSignerTests
{
    [Fact]
    public async Task PresignedPutObjectAsync_UsesConfiguredPublicEndpoint()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Minio:Endpoint"] = "minio:9000",
                ["Minio:PublicEndpoint"] = "http://localhost:9000",
                ["Minio:AccessKey"] = "test-access",
                ["Minio:SecretKey"] = "test-secret",
            })
            .Build();
        var signer = new BrowserObjectStorageSigner(configuration);

        var url = await signer.PresignedPutObjectAsync(new PresignedPutObjectArgs()
            .WithBucket("test-bucket")
            .WithObject("tenant/test/proof.pdf")
            .WithExpiry(300));

        Assert.StartsWith("http://localhost:9000/", url, StringComparison.Ordinal);
    }
}
