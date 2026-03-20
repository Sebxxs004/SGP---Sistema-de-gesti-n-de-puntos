using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class Purchase : TenantEntityBase
{
    public Guid BranchId { get; private set; }
    public Guid SupplierId { get; private set; }
    public DateTimeOffset PurchaseDate { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string? ReferenceNumber { get; private set; }

    public virtual Supplier? Supplier { get; private set; }
    public virtual ICollection<PurchaseItem> Items { get; private set; }

    private Purchase()
    {
        Items = new List<PurchaseItem>();
    }

    public Purchase(Guid tenantId, Guid branchId, Guid supplierId, DateTimeOffset purchaseDate, string? referenceNumber)
        : base(tenantId)
    {
        if (branchId == Guid.Empty) throw new ArgumentException("La sucursal es obligatoria.", nameof(branchId));
        if (supplierId == Guid.Empty) throw new ArgumentException("El proveedor es obligatorio.", nameof(supplierId));

        Id = Guid.NewGuid();
        BranchId = branchId;
        SupplierId = supplierId;
        PurchaseDate = purchaseDate;
        ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim();
        TotalAmount = 0m;
        Items = new List<PurchaseItem>();
    }

    public void AddItem(PurchaseItem item)
    {
        Items.Add(item);
        TotalAmount = Items.Sum(i => i.Quantity * i.UnitCost);
    }
}
