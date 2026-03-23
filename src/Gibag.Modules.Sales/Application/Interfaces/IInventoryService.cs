using Gibag.Shared.Models;

namespace Gibag.Modules.Sales.Application.Interfaces;

public interface IInventoryService
{
    Task<Result> CheckStockAsync(Guid branchId, Guid productId, decimal requestedQuantity, CancellationToken cancellationToken);
    Task<Result> DecrementStockAsync(Guid branchId, Guid productId, decimal quantity, string reference, CancellationToken cancellationToken);
    Task<Result> IncrementStockAsync(Guid branchId, Guid productId, decimal quantity, string reference, CancellationToken cancellationToken);
    Task<Dictionary<Guid, InventoryProductPricing>> GetProductPricingAsync(IEnumerable<Guid> productIds, CancellationToken cancellationToken);
}

public sealed record InventoryProductPricing(decimal UnitCost, decimal TaxRate);
