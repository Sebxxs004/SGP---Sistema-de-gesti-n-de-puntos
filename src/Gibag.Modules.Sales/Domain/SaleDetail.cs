using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public class SaleDetail : TenantEntityBase
{
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal SubTotal { get; private set; }

    public virtual Sale? Sale { get; private set; }

    private SaleDetail() {}

    public SaleDetail(Guid id, Guid tenantId, Guid saleId, Guid productId, decimal quantity, decimal unitPrice)
        : base(tenantId)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        SaleId = saleId;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        SubTotal = quantity * unitPrice;
    }
}
