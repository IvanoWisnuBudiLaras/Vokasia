using Microsoft.EntityFrameworkCore;
using Vokasia.Domain.Entities;
using Vokasia.Infrastructure.Persistence;
using Vokasia.Infrastructure.TenantContext;

namespace Vokasia.Tests.Security;

/// <summary>
/// AC VOK-H2-E3 (NFR-SEC-04): user tenant A tidak pernah bisa melihat data tenant B walau tahu ID-nya
/// — dibuktikan di level DbContext untuk resource inti yang dipakai H2-E1 endpoints (Period, Student,
/// Placement). Test HTTP end-to-end (lewat token JWT sungguhan, matrix RBAC penuh) tetap wilayah
/// H2-E3/H5-E3 (TenantIsolationTests via Testcontainers) — ini bukti mekanisme filter tidak bocor,
/// dijalankan lebih awal karena filter sudah diaktifkan penuh oleh TenantResolutionMiddleware H2-E3.
/// </summary>
public class TenantIsolationTests
{
    private static VokasiaDbContext CreateContext(AmbientTenantContext tenantContext, string dbName)
    {
        var options = new DbContextOptionsBuilder<VokasiaDbContext>().UseInMemoryDatabase(dbName).Options;
        return new VokasiaDbContext(options, tenantContext);
    }

    [Fact]
    public async Task Period_CrossTenantById_NeverVisible()
    {
        var dbName = Guid.NewGuid().ToString();
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var periodB = Guid.NewGuid();

        await using (var seedCtx = CreateContext(new AmbientTenantContext(), dbName))
        {
            seedCtx.Periods.Add(new Period { Id = periodB, TenantId = tenantB, Name = "Periode B", StartDate = DateOnly.MinValue, EndDate = DateOnly.MaxValue, ClassLevels = "XII" });
            await seedCtx.SaveChangesAsync();
        }

        await using var asTenantA = CreateContext(new AmbientTenantContext { TenantId = tenantA }, dbName);
        var found = await asTenantA.Periods.FirstOrDefaultAsync(p => p.Id == periodB);

        Assert.Null(found); // ID diketahui persis, tetap tidak terlihat lintas tenant.
    }

    [Fact]
    public async Task Placement_CrossTenantList_ExcludesOtherTenantRows()
    {
        var dbName = Guid.NewGuid().ToString();
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var periodId = Guid.NewGuid(); // sengaja ID periode SAMA di kedua tenant (uji filter, bukan periodId).

        await using (var seedCtx = CreateContext(new AmbientTenantContext(), dbName))
        {
            seedCtx.Placements.AddRange(
                new Placement { Id = Guid.NewGuid(), TenantId = tenantA, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = periodId, TeacherId = Guid.NewGuid() },
                new Placement { Id = Guid.NewGuid(), TenantId = tenantB, StudentId = Guid.NewGuid(), CompanyId = Guid.NewGuid(), PeriodId = periodId, TeacherId = Guid.NewGuid() });
            await seedCtx.SaveChangesAsync();
        }

        await using var asTenantA = CreateContext(new AmbientTenantContext { TenantId = tenantA }, dbName);
        var results = await asTenantA.Placements.Where(p => p.PeriodId == periodId).ToListAsync();

        Assert.Single(results);
        Assert.Equal(tenantA, results[0].TenantId);
    }

    [Fact]
    public async Task Student_CrossTenantById_NeverVisible_EvenWithCorrectMajorFilter()
    {
        var dbName = Guid.NewGuid().ToString();
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());
        var majorId = Guid.NewGuid();
        var studentB = Guid.NewGuid();

        await using (var seedCtx = CreateContext(new AmbientTenantContext(), dbName))
        {
            seedCtx.Students.Add(new Student { Id = studentB, TenantId = tenantB, FullName = "Siswa Tenant B", MajorId = majorId, Classroom = "XII" });
            await seedCtx.SaveChangesAsync();
        }

        await using var asTenantA = CreateContext(new AmbientTenantContext { TenantId = tenantA }, dbName);
        var results = await asTenantA.Students.Where(s => s.MajorId == majorId).ToListAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task TenantBoundLinkSlotAndInvoice_ExcludeRowsFromOtherTenant()
    {
        var dbName = Guid.NewGuid().ToString();
        var (tenantA, tenantB) = (Guid.NewGuid(), Guid.NewGuid());

        await using (var seedCtx = CreateContext(new AmbientTenantContext(), dbName))
        {
            seedCtx.TenantCompanies.AddRange(
                new TenantCompany
                {
                    TenantId = tenantA,
                    CompanyId = Guid.NewGuid(),
                },
                new TenantCompany
                {
                    TenantId = tenantB,
                    CompanyId = Guid.NewGuid(),
                });
            seedCtx.CompanySlots.AddRange(
                new CompanySlot
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantA,
                    CompanyId = Guid.NewGuid(),
                    PeriodId = Guid.NewGuid(),
                    Slots = 2,
                },
                new CompanySlot
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantB,
                    CompanyId = Guid.NewGuid(),
                    PeriodId = Guid.NewGuid(),
                    Slots = 3,
                });
            seedCtx.Invoices.AddRange(
                new Invoice
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantA,
                    PeriodMonth = new DateOnly(2026, 7, 1),
                    Amount = 100_000,
                },
                new Invoice
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantB,
                    PeriodMonth = new DateOnly(2026, 7, 1),
                    Amount = 200_000,
                });
            await seedCtx.SaveChangesAsync();
        }

        await using var asTenantA = CreateContext(
            new AmbientTenantContext { TenantId = tenantA },
            dbName);

        Assert.All(
            await asTenantA.TenantCompanies.ToListAsync(),
            row => Assert.Equal(tenantA, row.TenantId));
        Assert.All(
            await asTenantA.CompanySlots.ToListAsync(),
            row => Assert.Equal(tenantA, row.TenantId));
        Assert.All(
            await asTenantA.Invoices.ToListAsync(),
            row => Assert.Equal(tenantA, row.TenantId));
        Assert.Single(await asTenantA.TenantCompanies.ToListAsync());
        Assert.Single(await asTenantA.CompanySlots.ToListAsync());
        Assert.Single(await asTenantA.Invoices.ToListAsync());
    }

    [Fact]
    public void EveryMappedEntityWithRequiredTenantId_HasGlobalQueryFilter()
    {
        using var context = CreateContext(
            new AmbientTenantContext { TenantId = Guid.NewGuid() },
            Guid.NewGuid().ToString());

        var missingFilters = context.Model.GetEntityTypes()
            .Where(entity =>
                entity.FindProperty(nameof(Vokasia.Domain.Common.ITenantScoped.TenantId))
                    is { IsNullable: false })
            .Where(entity => !entity.GetDeclaredQueryFilters().Any())
            .Select(entity => entity.ClrType.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Empty(missingFilters);
    }

    [Fact]
    public async Task NullAmbientTenant_AllowsGlobalWorkerAndSuperAdminQueries()
    {
        // Null ambient tenant sengaja dipakai worker dan query global SuperAdmin. Endpoint tenant
        // tetap dilindungi RBAC yang mensyaratkan tenant_id claim.
        var dbName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();

        await using (var seedCtx = CreateContext(new AmbientTenantContext(), dbName))
        {
            seedCtx.Periods.Add(new Period { Id = Guid.NewGuid(), TenantId = tenantA, Name = "P", StartDate = DateOnly.MinValue, EndDate = DateOnly.MaxValue, ClassLevels = "XII" });
            await seedCtx.SaveChangesAsync();
        }

        await using var globalContext = CreateContext(new AmbientTenantContext { TenantId = null }, dbName);
        var all = await globalContext.Periods.ToListAsync();

        Assert.Single(all);
    }
}
