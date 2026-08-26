using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Vokasia.Tests.Fixtures;

public class VokasiaIntegrationFactory : WebApplicationFactory<Vokasia.Api.Program>, IAsyncLifetime
{
    private readonly string _dbConnectionString = "Host=localhost;Port=5432;Database=vokasia;Username=vokasia;Password=vokasia_dev";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((context, conf) =>
        {
            conf.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _dbConnectionString,
                ["Redis:Connection"] = "localhost:6379",
                ["RabbitMq:Host"] = "localhost",
                ["Minio:Endpoint"] = "localhost:9000",
                ["Smtp:Host"] = "localhost"
            });
        });
    }

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public new Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}
