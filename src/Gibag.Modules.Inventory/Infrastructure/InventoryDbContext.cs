using Gibag.Modules.Inventory.Domain;
using Gibag.Shared.Domain;
using Gibag.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Inventory.Infrastructure;

public class InventoryDbContext : DbContext
{
    private readonly ITenantService _tenantService;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantService tenantService) 
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<BranchStock> BranchStocks { get; set; } = null!;
    public DbSet<StockMovement> StockMovements { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<Purchase> Purchases { get; set; } = null!;
    public DbSet<PurchaseItem> PurchaseItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global Query Filter for Multi-Tenancy
        modelBuilder.Entity<Category>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<BranchStock>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);
        modelBuilder.Entity<Purchase>().HasQueryFilter(e => e.TenantId == _tenantService.CurrentTenantId);

        // Product Unique Indexes
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.TenantId, p.SKU })
            .IsUnique();
            
        modelBuilder.Entity<Product>()
            .HasIndex(p => new { p.TenantId, p.Barcode })
            .IsUnique();

        // BranchStock Unique Index
        modelBuilder.Entity<BranchStock>()
            .HasIndex(bs => new { bs.TenantId, bs.BranchId, bs.ProductId })
            .IsUnique();

        modelBuilder.Entity<Supplier>()
            .HasIndex(s => new { s.TenantId, s.Name });

        modelBuilder.Entity<Purchase>()
            .HasIndex(p => new { p.TenantId, p.BranchId, p.PurchaseDate });

        modelBuilder.Entity<Purchase>()
            .HasOne(p => p.Supplier)
            .WithMany(s => s.Purchases)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseItem>()
            .HasOne(pi => pi.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PurchaseItem>()
            .HasOne(pi => pi.Product)
            .WithMany()
            .HasForeignKey(pi => pi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PurchaseItem>()
            .HasIndex(pi => new { pi.PurchaseId, pi.ProductId });
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
            if (entry.Entity.TenantId == Guid.Empty)
            {
                if (currentTenantId == null || currentTenantId == Guid.Empty)
                {
                    throw new InvalidOperationException("Cannot save a TenantEntityBase without a valid CurrentTenantId in the context.");
                }

                entry.Property(x => x.TenantId).CurrentValue = currentTenantId.Value;
            }
        }
    }
}
