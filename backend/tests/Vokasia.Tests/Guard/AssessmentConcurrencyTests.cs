using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Tests.Guard;

public sealed class AssessmentConcurrencyTests
{
    [Fact]
    public void IsFinalIsConfiguredAsConcurrencyToken()
    {
        var options = new DbContextOptionsBuilder<VokasiaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new VokasiaDbContext(options, new AmbientTenantContext());

        var property = db.Model.FindEntityType(typeof(Vokasia.Domain.Entities.Assessment))?.FindProperty(nameof(Vokasia.Domain.Entities.Assessment.IsFinal));

        Assert.NotNull(property);
        Assert.True(property!.IsConcurrencyToken);
    }
}
