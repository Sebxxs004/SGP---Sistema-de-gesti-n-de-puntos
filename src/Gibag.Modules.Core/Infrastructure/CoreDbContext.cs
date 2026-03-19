using Gibag.Modules.Core.Domain;
using Gibag.Shared.Domain;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Core.Infrastructure;

public class CoreDbContext : DbContext
{
    private readonly ITenantService _tenantService;

    public CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantService tenantService) 
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Branch> Branches { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserBranch> UserBranches { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filter for Multi-Tenancy
        // We only apply this to entities that inherit from TenantEntityBase
        modelBuilder.Entity<Branch>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<Role>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<User>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<UserBranch>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);

        // Specific rules from db-core.md
        
        // Ensure Emails are unique per Tenant
        modelBuilder.Entity<User>()
            .HasIndex(u => new { u.TenantId, u.Email })
            .IsUnique();

        modelBuilder.Entity<Customer>()
            .HasIndex(c => new { c.TenantId, c.IsActive, c.Name });

        modelBuilder.Entity<Customer>()
            .HasIndex(c => new { c.TenantId, c.DocumentNumber });
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
        
        foreach (var entry in addedEntities)
        {
            // If the entity doesn't have a TenantId assigned manually, assign from the context
            if (entry.Entity.TenantId == Guid.Empty)
            {
                if (currentTenantId == null || currentTenantId == Guid.Empty)
                {
                    throw new InvalidOperationException("Cannot save a TenantEntityBase without a valid CurrentTenantId in the context.");
                }

                // We need to use reflection or property configuration if the setter is protected, 
                // but since we are modifying it from the DB context we can write directly to the EF Core CurrentValue
                entry.Property(x => x.TenantId).CurrentValue = currentTenantId.Value;
            }
        }
    }
}
