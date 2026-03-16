using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class Category : TenantEntityBase
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    // Navigation property
    public virtual ICollection<Product> Products { get; private set; }

    private Category() 
    {
        Name = string.Empty;
        Products = new List<Product>();
    }

    public Category(Guid tenantId, string name, string? description) : base(tenantId)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        IsActive = true;
        Products = new List<Product>();
    }

    public void Update(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
