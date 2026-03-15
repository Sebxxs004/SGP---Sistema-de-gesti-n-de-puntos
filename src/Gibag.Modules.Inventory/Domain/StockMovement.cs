using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class StockMovement : TenantEntityBase
{
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid UserId { get; private set; }
    public MovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; }
    public string? Reference { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation property
    public virtual Product? Product { get; private set; }

    private StockMovement() {}

    public StockMovement(Guid tenantId, Guid branchId, Guid productId, Guid userId, MovementType movementType, decimal quantity, string? reference)
        : base(tenantId)
    {
        Id = Guid.NewGuid();
        BranchId = branchId;
        ProductId = productId;
        UserId = userId;
        MovementType = movementType;
        Quantity = quantity;
        Reference = reference;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
