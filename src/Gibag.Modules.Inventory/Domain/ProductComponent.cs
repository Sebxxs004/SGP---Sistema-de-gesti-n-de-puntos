using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class ProductComponent : TenantEntityBase
{
    public Guid CompositeProductId { get; private set; }
    public Guid ComponentId { get; private set; }
    public decimal Quantity { get; private set; }

    public virtual Product? CompositeProduct { get; private set; }
    public virtual Product? Component { get; private set; }

    private ProductComponent() { }

    public ProductComponent(Guid tenantId, Guid compositeProductId, Guid componentId, decimal quantity)
        : base(tenantId)
    {
        if (compositeProductId == Guid.Empty) throw new ArgumentException("El producto compuesto es obligatorio.", nameof(compositeProductId));
        if (componentId == Guid.Empty) throw new ArgumentException("El componente es obligatorio.", nameof(componentId));
        if (quantity <= 0m) throw new ArgumentException("La cantidad del componente debe ser mayor a cero.", nameof(quantity));

        Id = Guid.NewGuid();
        CompositeProductId = compositeProductId;
        ComponentId = componentId;
        Quantity = quantity;
    }
}
