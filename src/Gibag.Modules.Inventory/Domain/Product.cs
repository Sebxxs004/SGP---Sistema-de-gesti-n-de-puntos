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
    public decimal TaxRate { get; private set; }
    public decimal MinStockLevel { get; private set; }
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

    public Product(Guid tenantId, Guid categoryId, string name, string sku, string? barcode, decimal basePrice, decimal cost, decimal minStockLevel = 5m, bool isComposite = false, decimal taxRate = 0m) 
        : base(tenantId)
    {
        Id = Guid.NewGuid();
        CategoryId = categoryId;
        Name = name;
        SKU = sku;
        Barcode = barcode;
        BasePrice = basePrice;
        Cost = cost;
        TaxRate = NormalizeTaxRate(taxRate);
        MinStockLevel = minStockLevel < 0m ? 0m : minStockLevel;
        IsComposite = isComposite;
        IsActive = true;
        BranchStocks = new List<BranchStock>();
        Components = new List<ProductComponent>();
        UsedInComposites = new List<ProductComponent>();
    }

    public void Update(Guid categoryId, string name, string sku, string? barcode, decimal basePrice, decimal cost, decimal minStockLevel, bool isComposite, decimal taxRate)
    {
        CategoryId = categoryId;
        Name = name;
        SKU = sku;
        Barcode = barcode;
        BasePrice = basePrice;
        Cost = cost;
        TaxRate = NormalizeTaxRate(taxRate);
        MinStockLevel = minStockLevel < 0m ? 0m : minStockLevel;
        IsComposite = isComposite;
    }

    private static decimal NormalizeTaxRate(decimal taxRate)
    {
        if (taxRate < 0m)
        {
            return 0m;
        }

        if (taxRate > 100m)
        {
            return 100m;
        }

        return taxRate;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
