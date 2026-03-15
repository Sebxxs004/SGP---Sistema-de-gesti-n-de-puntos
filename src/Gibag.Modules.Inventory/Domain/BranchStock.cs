using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class BranchStock : TenantEntityBase
{
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal MinStockLevel { get; private set; }

    // Navigation property
    public virtual Product? Product { get; private set; }
    // En CQRS modular, la Branch viaja como un ID externo o hay referencia cruzada mínima. No referenciamos el módulo Core.Domain directo.

    private BranchStock() {}

    public BranchStock(Guid tenantId, Guid branchId, Guid productId, decimal quantity, decimal minStockLevel) 
        : base(tenantId)
    {
        Id = Guid.NewGuid();
        BranchId = branchId;
        ProductId = productId;
        Quantity = quantity;
        MinStockLevel = minStockLevel;
    }
}
