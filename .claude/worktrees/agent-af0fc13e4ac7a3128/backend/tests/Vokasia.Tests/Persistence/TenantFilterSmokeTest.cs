using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Tests.Persistence;

/// <summary>
/// Bukti mekanis untuk AC VOK-H1-E1: global query filter tenant hidup (walau ITenantContext
/// masih diisi manual — middleware nyata menyusul H2-E3). InMemory provider dipakai agar cepat
/// & tanpa dependency Docker untuk smoke test level ini.
/// </summary>
public class TenantFilterSmokeTest
{
    private static VokasiaDbContext CreateContext(AmbientTenantContext tenantContext, string dbName)
    {
        var options = new DbContextOptionsBuilder<VokasiaDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new VokasiaDbContext(options, tenantContext);
    }

    [Fact]
    public async Task Query_WithoutTenantContext_ReturnsAllRows_ButWithTenantContext_IsScoped()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Seed via a context with no tenant restriction (simulates SuperAdmin/seeder write).
        await using (var seedCtx = CreateContext(new AmbientTenantContext(), dbName))
        {
            seedCtx.Majors.Add(new Major { Id = Guid.NewGuid(), TenantId = tenantA, Name = "TKJ" });
            seedCtx.Majors.Add(new Major { Id = Guid.NewGuid(), TenantId = tenantB, Name = "RPL" });
            await seedCtx.SaveChangesAsync();
        }

        // Given: request context scoped to tenant A.
        await using var scopedCtx = CreateContext(new AmbientTenantContext { TenantId = tenantA }, dbName);

        // When: query without an explicit tenant predicate.
        var result = await scopedCtx.Majors.ToListAsync();

        // Then: only tenant A's row is visible — proves the global filter is mechanically active.
        Assert.Single(result);
        Assert.Equal(tenantA, result[0].TenantId);
    }

    [Fact]
    public async Task Query_CrossTenantId_NeverReturnsOtherTenantRow()
    {
        var dbName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var studentBId = Guid.NewGuid();

        await using (var seedCtx = CreateContext(new AmbientTenantContext(), dbName))
        {
            seedCtx.Students.Add(new Student { Id = studentBId, TenantId = tenantB, FullName = "Siswa B", MajorId = Guid.NewGuid(), Classroom = "XII" });
            await seedCtx.SaveChangesAsync();
        }

        await using var scopedAsA = CreateContext(new AmbientTenantContext { TenantId = tenantA }, dbName);

        var found = await scopedAsA.Students.FirstOrDefaultAsync(x => x.Id == studentBId);

        Assert.Null(found); // tenant A tidak pernah melihat data tenant B, walau ID diketahui.
    }
}
