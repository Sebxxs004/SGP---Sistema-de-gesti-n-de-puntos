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

            logger.LogInformation("[Seed] Migrations applied.");

            // ── 2. Idempotency guard ─────────────────────────────────────────
            if (await coreDb.Tenants.AnyAsync(t => t.Id == SeedTenantId))
            {
                logger.LogInformation("[Seed] Seed data already present, skipping.");
                return;
            }

            // ── 3. Tenant ────────────────────────────────────────────────────
            var tenant = new Tenant("SGP Demo", "000-000-0000-0", "Free");
            // Override the auto-generated Id with the fixed seed Id via EF property
            coreDb.Entry(tenant).Property("Id").CurrentValue = SeedTenantId;
            await coreDb.Tenants.AddAsync(tenant);

            // ── 4. Branch ────────────────────────────────────────────────────
            var branch = new Branch(SeedTenantId, "Sucursal Central", "Av. Principal 123", "America/Bogota");
            coreDb.Entry(branch).Property("Id").CurrentValue = SeedBranchId;
            await coreDb.Branches.AddAsync(branch);

            // ── 5. Role ──────────────────────────────────────────────────────
            var role = new Role(SeedTenantId, "Admin", "[\"all\"]");
            coreDb.Entry(role).Property("Id").CurrentValue = SeedRoleId;
            await coreDb.Roles.AddAsync(role);

            // ── 6. User ──────────────────────────────────────────────────────
            // Password: Admin123!
            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Admin123!");
            var user = new User(SeedTenantId, SeedRoleId, "admin@sgp.com", hashedPassword, "Admin", "SGP");
            coreDb.Entry(user).Property("Id").CurrentValue = SeedUserId;
            await coreDb.Users.AddAsync(user);

            // ── 7. UserBranch ────────────────────────────────────────────────
            var userBranch = new UserBranch(SeedTenantId, SeedUserId, SeedBranchId, isPrimary: true);
            await coreDb.UserBranches.AddAsync(userBranch);

            await coreDb.SaveChangesAsync();
            logger.LogInformation("[Seed] Core entities committed.");

            // ── 8. Default Inventory Category ────────────────────────────────
            var category = new Category(SeedTenantId, "General", "Categoría por defecto");
            inventoryDb.Entry(category).Property("Id").CurrentValue = SeedCategoryId;
            await inventoryDb.Categories.AddAsync(category);
            await inventoryDb.SaveChangesAsync();

            logger.LogInformation("[Seed] Inventory category committed.");
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
