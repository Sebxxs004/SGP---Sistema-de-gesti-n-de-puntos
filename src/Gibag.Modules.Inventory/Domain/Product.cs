using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class Product : TenantEntityBase
{
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; }
    public string SKU { get; private set; }
    public string? Barcode { get; private set; }
    public decimal BasePrice { get; private set; }
    public decimal Cost { get; private set; }
    public bool IsComposite { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation properties
    public virtual Category? Category { get; private set; }
    public virtual ICollection<BranchStock> BranchStocks { get; private set; }
    public virtual ICollection<ProductComponent> Components { get; private set; }
    public virtual ICollection<ProductComponent> UsedInComposites { get; private set; }

    private Product() 
    {
        Name = string.Empty;
        SKU = string.Empty;
        BranchStocks = new List<BranchStock>();
        Components = new List<ProductComponent>();
        UsedInComposites = new List<ProductComponent>();
    }

    public Product(Guid tenantId, Guid categoryId, string name, string sku, string? barcode, decimal basePrice, decimal cost, bool isComposite = false) 
        : base(tenantId)
    {
        Id = Guid.NewGuid();
        CategoryId = categoryId;
        Name = name;
        SKU = sku;
        Barcode = barcode;
        BasePrice = basePrice;
        Cost = cost;
        IsComposite = isComposite;
        IsActive = true;
        BranchStocks = new List<BranchStock>();
        Components = new List<ProductComponent>();
        UsedInComposites = new List<ProductComponent>();
    }

    public void Update(Guid categoryId, string name, string sku, string? barcode, decimal basePrice, decimal cost, bool isComposite)
    {
        CategoryId = categoryId;
        Name = name;
        SKU = sku;
        Barcode = barcode;
        BasePrice = basePrice;
        Cost = cost;
        IsComposite = isComposite;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
