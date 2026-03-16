using Gibag.Modules.Sales.Domain;
using Gibag.Shared.Domain;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Sales.Infrastructure;

public class SalesDbContext : DbContext
{
    private readonly ITenantService _tenantService;

    public SalesDbContext(DbContextOptions<SalesDbContext> options, ITenantService tenantService) 
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<CashRegisterSession> CashRegisterSessions { get; set; } = null!;
    public DbSet<CashMovement> CashMovements { get; set; } = null!;
    public DbSet<Sale> Sales { get; set; } = null!;
    public DbSet<SaleDetail> SaleDetails { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filter for Multi-Tenancy
        modelBuilder.Entity<CashRegisterSession>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<CashMovement>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<Sale>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<SaleDetail>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<Payment>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);

        // Constraints and indexes
        modelBuilder.Entity<Sale>()
            .HasIndex(s => new { s.TenantId, s.SessionId });
            
        modelBuilder.Entity<CashRegisterSession>()
            .HasIndex(crs => new { crs.TenantId, crs.BranchId });

        modelBuilder.Entity<CashMovement>()
            .HasIndex(cm => new { cm.TenantId, cm.SessionId, cm.CreatedAt });
    }

    public override int SaveChanges()
    {
        ApplyTenantIdToEntities();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantIdToEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyTenantIdToEntities()
    {
        var addedEntities = ChangeTracker.Entries<TenantEntityBase>()
            .Where(e => e.State == EntityState.Added)
            .ToList();

        if (addedEntities.Count == 0) return;

        var currentTenantId = _tenantService.CurrentTenantId;
        if (currentTenantId == null || currentTenantId == Guid.Empty)
        {
            throw new InvalidOperationException("Cannot save a TenantEntityBase without a valid CurrentTenantId in the context.");
        }

        foreach (var entry in addedEntities)
        {
            if (entry.Entity.TenantId == Guid.Empty)
            {
                entry.Property(x => x.TenantId).CurrentValue = currentTenantId.Value;
            }
        }
    }
}
