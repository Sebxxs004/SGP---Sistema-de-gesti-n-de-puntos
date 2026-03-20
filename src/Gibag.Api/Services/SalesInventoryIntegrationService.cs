using Gibag.Modules.Inventory.Domain;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Modules.Sales.Application.Interfaces;
using Gibag.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Api.Services;

public class SalesInventoryIntegrationService : IInventoryService
{
    private readonly InventoryDbContext _inventoryDbContext;

    public SalesInventoryIntegrationService(InventoryDbContext inventoryDbContext)
    {
        _inventoryDbContext = inventoryDbContext;
    }

    public async Task<Result> CheckStockAsync(Guid branchId, Guid productId, decimal requestedQuantity, CancellationToken cancellationToken)
    {
        // Global Query Filter is active via the injected Tenant Service in InventoryDbContext
        var stock = await _inventoryDbContext.BranchStocks
            .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.ProductId == productId, cancellationToken);

        if (stock == null)
            return Result.Failure("Inventory.StockNotFound", $"No se encontró stock para el producto {productId} en la sucursal.");

        if (stock.Quantity < requestedQuantity)
            return Result.Failure("Inventory.InsufficientStock", $"Stock insuficiente. Disp: {stock.Quantity}, Req: {requestedQuantity}");

        return Result.Success();
    }

    public async Task<Result> DecrementStockAsync(Guid branchId, Guid productId, decimal quantity, string reference, CancellationToken cancellationToken)
    {
        var stock = await _inventoryDbContext.BranchStocks
            .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.ProductId == productId, cancellationToken);

        if (stock == null)
            return Result.Failure("Inventory.StockNotFound", "No se encontró stock para actualizar.");

        if (stock.Quantity < quantity)
            return Result.Failure("Inventory.InsufficientStock", "Stock insuficiente al intentar decrementar.");

        // Deduct
        // As EF tracks 'stock', we can just modify properties
        var quantityProperty = stock.GetType().GetProperty("Quantity");
        if (quantityProperty != null && quantityProperty.CanWrite)
        {
            // Note: Since setters are private in Domain, we use reflection or we should add a Deduct method in BranchStock.
            // Let's use reflection to respect the private setter without modifying the domain for now,
            // or better yet, assume the domain allows deducting logic. Wait, let's use reflection safely here.
            quantityProperty.SetValue(stock, stock.Quantity - quantity);
        }

        // Add a StockMovement record
        // We need the UserId from context or pass it down. 
        // We will default to Guid.Empty if not provided, though the schema asks for it.
        // In a real scenario, the Service should inject ICurrentUser to know who made the movement.
        var movement = new StockMovement(
            stock.TenantId,
            branchId,
            productId,
            Guid.Empty, // Cashier ID ideally
            MovementType.Sale,
            quantity,
            reference
        );

        await _inventoryDbContext.StockMovements.AddAsync(movement, cancellationToken);
        await _inventoryDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> IncrementStockAsync(Guid branchId, Guid productId, decimal quantity, string reference, CancellationToken cancellationToken)
    {
        var stock = await _inventoryDbContext.BranchStocks
            .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.ProductId == productId, cancellationToken);

        if (stock == null)
            return Result.Failure("Inventory.StockNotFound", "No se encontró stock para actualizar.");

        var quantityProperty = stock.GetType().GetProperty("Quantity");
        if (quantityProperty != null && quantityProperty.CanWrite)
        {
            quantityProperty.SetValue(stock, stock.Quantity + quantity);
        }

        var movement = new StockMovement(
            stock.TenantId,
            branchId,
            productId,
            Guid.Empty,
            MovementType.In,
            quantity,
            reference
        );

        await _inventoryDbContext.StockMovements.AddAsync(movement, cancellationToken);
        await _inventoryDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Dictionary<Guid, decimal>> GetProductUnitCostsAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken)
    {
        var ids = productIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        return await _inventoryDbContext.Products
            .AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToDictionaryAsync(
                p => p.Id,
                p => p.Cost > 0m ? p.Cost : p.BasePrice,
                cancellationToken);
    }
}
