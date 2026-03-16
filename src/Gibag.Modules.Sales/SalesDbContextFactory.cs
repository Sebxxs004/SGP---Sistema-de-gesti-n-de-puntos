using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gibag.Modules.Sales;

/// <summary>
/// IDesignTimeDbContextFactory for SalesDbContext.
/// Enables `dotnet ef migrations add` without a running host.
/// </summary>
public class SalesDbContextFactory : IDesignTimeDbContextFactory<SalesDbContext>
{
    public SalesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SalesDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=gibag_db;Username=gibag_user;Password=gibag_password");

        return new SalesDbContext(optionsBuilder.Options, new DesignTimeTenantService());
    }
}

internal sealed class DesignTimeTenantService : ITenantService
{
    public Guid? CurrentTenantId => Guid.Empty;
    public void SetCurrentTenantId(Guid tenantId) { }
}
