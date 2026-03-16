using Gibag.Modules.Core.Domain;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Inventory.Domain;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Modules.Sales.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Api.Infrastructure;

public static class DbInitializer
{
    // Fixed seed IDs – these are the credentials for development login
    public static readonly Guid SeedTenantId   = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid SeedBranchId   = new("00000000-0000-0000-0000-000000000002");
    public static readonly Guid SeedRoleId     = new("00000000-0000-0000-0000-000000000003");
    public static readonly Guid SeedUserId     = new("00000000-0000-0000-0000-000000000004");
    public static readonly Guid SeedCategoryId = new("00000000-0000-0000-0000-000000000005");
    public static readonly Guid SeedCategoryMerchId = new("00000000-0000-0000-0000-000000000006");
    public static readonly Guid SeedCategoryBakeryId = new("00000000-0000-0000-0000-000000000007");

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            // ── 1. Apply all pending EF Core migrations ──────────────────────
            logger.LogInformation("[Seed] Applying migrations...");

            var coreDb = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
            await coreDb.Database.MigrateAsync();

            var inventoryDb = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await inventoryDb.Database.MigrateAsync();

            var salesDb = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
            await salesDb.Database.MigrateAsync();

            // Ensure optional columns introduced in later phases exist in databases
            await coreDb.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Branches""
                ADD COLUMN IF NOT EXISTS ""Phone"" text NULL;
            ");

            await coreDb.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Tenants""
                ADD COLUMN IF NOT EXISTS ""ThankYouMessage"" text NULL;
            ");

            await coreDb.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Tenants""
                ADD COLUMN IF NOT EXISTS ""TaxPercentage"" numeric NOT NULL DEFAULT 16;
            ");

            await coreDb.Database.ExecuteSqlRawAsync(@"
                ALTER TABLE ""Tenants""
                ADD COLUMN IF NOT EXISTS ""CurrencySymbol"" text NOT NULL DEFAULT '$';
            ");

            logger.LogInformation("[Seed] Migrations applied.");

            // ── 2. Seed Core entities once ──────────────────────────────────
            var tenantExists = await coreDb.Tenants.AnyAsync(t => t.Id == SeedTenantId);

            if (!tenantExists)
            {
                // ── 3. Tenant ────────────────────────────────────────────────
                var tenant = new Tenant("SGP Demo", "000-000-0000-0", "Free");
                tenant.UpdateThankYouMessage("Gracias por preferirnos");
                tenant.UpdateFinancialSettings(16m, "$" );
                // Override the auto-generated Id with the fixed seed Id via EF property
                coreDb.Entry(tenant).Property("Id").CurrentValue = SeedTenantId;
                await coreDb.Tenants.AddAsync(tenant);

                // ── 4. Branch ────────────────────────────────────────────────
                var branch = new Branch(SeedTenantId, "Sucursal Central", "Av. Principal 123", "America/Bogota", "+57 300 000 0000");
                coreDb.Entry(branch).Property("Id").CurrentValue = SeedBranchId;
                await coreDb.Branches.AddAsync(branch);

                // ── 5. Role ──────────────────────────────────────────────────
                var role = new Role(SeedTenantId, "Admin", "[\"all\"]");
                coreDb.Entry(role).Property("Id").CurrentValue = SeedRoleId;
                await coreDb.Roles.AddAsync(role);

                // ── 6. User ──────────────────────────────────────────────────
                // Password: Admin123!
                var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Admin123!");
                var user = new User(SeedTenantId, SeedRoleId, "admin@sgp.com", hashedPassword, "Admin", "SGP");
                coreDb.Entry(user).Property("Id").CurrentValue = SeedUserId;
                await coreDb.Users.AddAsync(user);

                // ── 7. UserBranch ────────────────────────────────────────────
                var userBranch = new UserBranch(SeedTenantId, SeedUserId, SeedBranchId, isPrimary: true);
                await coreDb.UserBranches.AddAsync(userBranch);

                await coreDb.SaveChangesAsync();
                logger.LogInformation("[Seed] Core entities committed.");
            }
            else
            {
                logger.LogInformation("[Seed] Core entities already present.");

                var existingTenant = await coreDb.Tenants.FirstOrDefaultAsync(t => t.Id == SeedTenantId);
                if (existingTenant != null && string.IsNullOrWhiteSpace(existingTenant.ThankYouMessage))
                {
                    existingTenant.UpdateThankYouMessage("Gracias por preferirnos");
                    await coreDb.SaveChangesAsync();
                }

                if (existingTenant != null)
                {
                    var currencySymbol = string.IsNullOrWhiteSpace(existingTenant.CurrencySymbol)
                        ? "$"
                        : existingTenant.CurrencySymbol;

                    var taxPercentage = existingTenant.TaxPercentage <= 0 ? 16m : existingTenant.TaxPercentage;

                    if (currencySymbol != existingTenant.CurrencySymbol || taxPercentage != existingTenant.TaxPercentage)
                    {
                        existingTenant.UpdateFinancialSettings(taxPercentage, currencySymbol);
                        await coreDb.SaveChangesAsync();
                    }
                }

                var existingBranch = await coreDb.Branches
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(b => b.Id == SeedBranchId);
                if (existingBranch != null && string.IsNullOrWhiteSpace(existingBranch.Phone))
                {
                    existingBranch.Update(existingBranch.Name, existingBranch.Address, existingBranch.Timezone, "+57 300 000 0000");
                    await coreDb.SaveChangesAsync();
                }
            }

            // ── 8. Inventory Categories + Products + BranchStock (idempotent) ──
            var categories = new[]
            {
                new { Id = SeedCategoryId, Name = "Cafetería", Description = "Bebidas y cafe" },
                new { Id = SeedCategoryMerchId, Name = "Merch", Description = "Merchandising" },
                new { Id = SeedCategoryBakeryId, Name = "Pastelería", Description = "Panaderia y snacks" },
            };

            foreach (var categorySeed in categories)
            {
                var categoryExists = await inventoryDb.Categories
                    .IgnoreQueryFilters()
                    .AnyAsync(c => c.TenantId == SeedTenantId && c.Id == categorySeed.Id);

                if (categoryExists) continue;

                var category = new Category(SeedTenantId, categorySeed.Name, categorySeed.Description);
                inventoryDb.Entry(category).Property("Id").CurrentValue = categorySeed.Id;
                await inventoryDb.Categories.AddAsync(category);
            }

            await inventoryDb.SaveChangesAsync();

            var productSeeds = new[]
            {
                new { Sku = "PRD-001", Name = "Café de Especialidad 500g", CategoryId = SeedCategoryId, Price = 12.50m, Cost = 8.00m, Qty = 45m },
                new { Sku = "PRD-002", Name = "Taza SGP Pro", CategoryId = SeedCategoryMerchId, Price = 8.90m, Cost = 4.50m, Qty = 12m },
                new { Sku = "PRD-003", Name = "Leche de Almendras 1L", CategoryId = SeedCategoryId, Price = 3.20m, Cost = 2.10m, Qty = 4m },
                new { Sku = "PRD-004", Name = "Galletas de Avena", CategoryId = SeedCategoryBakeryId, Price = 2.10m, Cost = 1.00m, Qty = 0m },
            };

            foreach (var productSeed in productSeeds)
            {
                var product = await inventoryDb.Products
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(p => p.TenantId == SeedTenantId && p.SKU == productSeed.Sku);

                if (product == null)
                {
                    product = new Product(
                        SeedTenantId,
                        productSeed.CategoryId,
                        productSeed.Name,
                        productSeed.Sku,
                        barcode: null,
                        basePrice: productSeed.Price,
                        cost: productSeed.Cost);

                    await inventoryDb.Products.AddAsync(product);
                    await inventoryDb.SaveChangesAsync();
                }

                var branchStockExists = await inventoryDb.BranchStocks
                    .IgnoreQueryFilters()
                    .AnyAsync(bs => bs.TenantId == SeedTenantId && bs.BranchId == SeedBranchId && bs.ProductId == product.Id);

                if (!branchStockExists)
                {
                    var branchStock = new BranchStock(SeedTenantId, SeedBranchId, product.Id, productSeed.Qty, minStockLevel: 5m);
                    await inventoryDb.BranchStocks.AddAsync(branchStock);
                }
            }

            await inventoryDb.SaveChangesAsync();
            logger.LogInformation("[Seed] Inventory catalog committed.");
            logger.LogInformation("══════════════════════════════════════════");
            logger.LogInformation("[Seed]  TENANT ID : {TenantId}", SeedTenantId);
            logger.LogInformation("[Seed]  EMAIL     : admin@sgp.com");
            logger.LogInformation("[Seed]  PASSWORD  : Admin123!");
            logger.LogInformation("══════════════════════════════════════════");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Seed] Error seeding database.");
            throw;
        }
    }
}
