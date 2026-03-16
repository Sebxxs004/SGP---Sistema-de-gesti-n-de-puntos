using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Gibag.Modules.Core;

/// <summary>
/// DesignTimeDbContextFactory for CoreDbContext.
/// Required so `dotnet ef migrations add` can instantiate CoreDbContext
/// without a running host (since CoreDbContext requires ITenantService).
/// </summary>
public class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CoreDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=gibag_db;Username=gibag_user;Password=gibag_password");

        // Provide a no-op ITenantService for design-time migrations
        return new CoreDbContext(optionsBuilder.Options, new DesignTimeTenantService());
    }
}

/// <summary>Minimal ITenantService used only at design time when generating migrations.</summary>
internal sealed class DesignTimeTenantService : ITenantService
{
    public Guid? CurrentTenantId => Guid.Empty;
    public Guid? CurrentBranchId => Guid.Empty;
    public void SetCurrentTenantId(Guid tenantId) { }
    public void SetCurrentBranchId(Guid branchId) { }
}
