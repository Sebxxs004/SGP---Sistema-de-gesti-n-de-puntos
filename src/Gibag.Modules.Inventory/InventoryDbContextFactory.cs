using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gibag.Modules.Inventory;

/// <summary>
/// IDesignTimeDbContextFactory for InventoryDbContext.
/// Enables `dotnet ef migrations add` without a running host.
/// </summary>
public class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InventoryDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=gibag_db;Username=gibag_user;Password=gibag_password");

        return new InventoryDbContext(optionsBuilder.Options, new DesignTimeTenantService());
    }
}

internal sealed class DesignTimeTenantService : ITenantService
{
    public Guid? CurrentTenantId => Guid.Empty;
    public Guid? CurrentBranchId => Guid.Empty;
    public void SetCurrentTenantId(Guid tenantId) { }
    public void SetCurrentBranchId(Guid branchId) { }
}
