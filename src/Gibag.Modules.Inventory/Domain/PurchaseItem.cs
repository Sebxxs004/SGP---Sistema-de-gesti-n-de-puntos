using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class PurchaseItem : EntityBase
{
    public Guid PurchaseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }

    public virtual Purchase? Purchase { get; private set; }
    public virtual Product? Product { get; private set; }

    private PurchaseItem() { }

    public PurchaseItem(Guid purchaseId, Guid productId, decimal quantity, decimal unitCost)
    {
        if (purchaseId == Guid.Empty) throw new ArgumentException("La compra es obligatoria.", nameof(purchaseId));
        if (productId == Guid.Empty) throw new ArgumentException("El producto es obligatorio.", nameof(productId));
        if (quantity <= 0m) throw new ArgumentException("La cantidad debe ser mayor a cero.", nameof(quantity));
        if (unitCost < 0m) throw new ArgumentException("El costo unitario no puede ser negativo.", nameof(unitCost));

        Id = Guid.NewGuid();
        PurchaseId = purchaseId;
        ProductId = productId;
        Quantity = quantity;
        UnitCost = unitCost;
    }
}
