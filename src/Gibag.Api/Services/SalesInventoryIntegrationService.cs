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
        return await CheckStockRecursiveAsync(branchId, productId, requestedQuantity, new HashSet<Guid>(), cancellationToken);
    }

    public async Task<Result> DecrementStockAsync(Guid branchId, Guid productId, decimal quantity, string reference, CancellationToken cancellationToken)
    {
        var checkResult = await CheckStockAsync(branchId, productId, quantity, cancellationToken);
        if (checkResult.IsFailure)
        {
            return checkResult;
        }

        var decrementResult = await DecrementStockRecursiveAsync(branchId, productId, quantity, reference, new HashSet<Guid>(), cancellationToken);
        if (decrementResult.IsFailure)
        {
            return decrementResult;
        }

        await _inventoryDbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> IncrementStockAsync(Guid branchId, Guid productId, decimal quantity, string reference, CancellationToken cancellationToken)
    {
        var incrementResult = await IncrementStockRecursiveAsync(branchId, productId, quantity, reference, new HashSet<Guid>(), cancellationToken);
        if (incrementResult.IsFailure)
        {
            return incrementResult;
        }

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

    private async Task<Result> CheckStockRecursiveAsync(Guid branchId, Guid productId, decimal requestedQuantity, HashSet<Guid> path, CancellationToken cancellationToken)
    {
        var product = await _inventoryDbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            return Result.Failure("Inventory.ProductNotFound", $"No se encontró el producto {productId}.");
        }

        if (!path.Add(productId))
        {
            return Result.Failure("Inventory.CompositeCycle", "Se detectó una referencia circular en la receta de productos compuestos.");
        }

        try
        {
            if (!product.IsComposite)
            {
                var stock = await _inventoryDbContext.BranchStocks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.ProductId == productId, cancellationToken);

                if (stock == null)
                    return Result.Failure("Inventory.StockNotFound", $"No se encontró stock para el producto {productId} en la sucursal.");

                if (stock.Quantity < requestedQuantity)
                    return Result.Failure("Inventory.InsufficientStock", $"Stock insuficiente. Disp: {stock.Quantity}, Req: {requestedQuantity}");

                return Result.Success();
            }

            var components = await _inventoryDbContext.ProductComponents
                .AsNoTracking()
                .Where(pc => pc.CompositeProductId == productId)
                .ToListAsync(cancellationToken);

            if (components.Count == 0)
            {
                return Result.Failure("Inventory.ComponentsRequired", $"El producto compuesto {product.Name} no tiene ingredientes configurados.");
            }

            foreach (var component in components)
            {
                var requiredQuantity = component.Quantity * requestedQuantity;
                var componentCheck = await CheckStockRecursiveAsync(branchId, component.ComponentId, requiredQuantity, path, cancellationToken);
                if (componentCheck.IsFailure)
                {
                    return componentCheck;
                }
            }

            return Result.Success();
        }
        finally
        {
            path.Remove(productId);
        }
    }

    private async Task<Result> DecrementStockRecursiveAsync(Guid branchId, Guid productId, decimal quantity, string reference, HashSet<Guid> path, CancellationToken cancellationToken)
    {
        var product = await _inventoryDbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            return Result.Failure("Inventory.ProductNotFound", $"No se encontró el producto {productId}.");
        }

        if (!path.Add(productId))
        {
            return Result.Failure("Inventory.CompositeCycle", "Se detectó una referencia circular en la receta de productos compuestos.");
        }

        try
        {
            if (!product.IsComposite)
            {
                var stock = await _inventoryDbContext.BranchStocks
                    .FirstOrDefaultAsync(bs => bs.BranchId == branchId && bs.ProductId == productId, cancellationToken);

                if (stock == null)
                    return Result.Failure("Inventory.StockNotFound", "No se encontró stock para actualizar.");

                if (stock.Quantity < quantity)
                    return Result.Failure("Inventory.InsufficientStock", "Stock insuficiente al intentar decrementar.");

                var quantityProperty = stock.GetType().GetProperty("Quantity");
                if (quantityProperty != null && quantityProperty.CanWrite)
                {
                    quantityProperty.SetValue(stock, stock.Quantity - quantity);
                }

                await _inventoryDbContext.StockMovements.AddAsync(new StockMovement(
                    stock.TenantId,
                    branchId,
                    productId,
                    Guid.Empty,
                    MovementType.Sale,
                    quantity,
                    reference), cancellationToken);

                return Result.Success();
            }

            var components = await _inventoryDbContext.ProductComponents
                .Where(pc => pc.CompositeProductId == productId)
                .ToListAsync(cancellationToken);

            if (components.Count == 0)
            {
                return Result.Failure("Inventory.ComponentsRequired", $"El producto compuesto {product.Name} no tiene ingredientes configurados.");
            }

            foreach (var component in components)
            {
                var requiredQuantity = component.Quantity * quantity;
                var decrement = await DecrementStockRecursiveAsync(
                    branchId,
                    component.ComponentId,
                    requiredQuantity,
                    $"{reference} | Comp:{product.Name}",
                    path,
                    cancellationToken);

                if (decrement.IsFailure)
                {
                    return decrement;
                }
            }

            return Result.Success();
        }
        finally
        {
            path.Remove(productId);
        }
    }

    private async Task<Result> IncrementStockRecursiveAsync(Guid branchId, Guid productId, decimal quantity, string reference, HashSet<Guid> path, CancellationToken cancellationToken)
    {
        var product = await _inventoryDbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product == null)
        {
            return Result.Failure("Inventory.ProductNotFound", $"No se encontró el producto {productId}.");
        }

        if (!path.Add(productId))
        {
            return Result.Failure("Inventory.CompositeCycle", "Se detectó una referencia circular en la receta de productos compuestos.");
        }

        try
        {
            if (!product.IsComposite)
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

                await _inventoryDbContext.StockMovements.AddAsync(new StockMovement(
                    stock.TenantId,
                    branchId,
                    productId,
                    Guid.Empty,
                    MovementType.In,
                    quantity,
                    reference), cancellationToken);

                return Result.Success();
            }

            var components = await _inventoryDbContext.ProductComponents
                .Where(pc => pc.CompositeProductId == productId)
                .ToListAsync(cancellationToken);

            if (components.Count == 0)
            {
                return Result.Failure("Inventory.ComponentsRequired", $"El producto compuesto {product.Name} no tiene ingredientes configurados.");
            }

            foreach (var component in components)
            {
                var requiredQuantity = component.Quantity * quantity;
                var increment = await IncrementStockRecursiveAsync(
                    branchId,
                    component.ComponentId,
                    requiredQuantity,
                    $"{reference} | Reverso comp:{product.Name}",
                    path,
                    cancellationToken);

                if (increment.IsFailure)
                {
                    return increment;
                }
            }

            return Result.Success();
        }
        finally
        {
            path.Remove(productId);
        }
    }
}
