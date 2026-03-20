using Gibag.Shared.Domain;

namespace Gibag.Modules.Inventory.Domain;

public class Supplier : TenantEntityBase
{
    public string Name { get; private set; }
    public string? ContactName { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }

    public virtual ICollection<Purchase> Purchases { get; private set; }

    private Supplier()
    {
        Name = string.Empty;
        Purchases = new List<Purchase>();
    }

    public Supplier(Guid tenantId, string name, string? contactName, string? phone, string? email, string? address)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del proveedor es obligatorio.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        Purchases = new List<Purchase>();
    }

    public void Update(string name, string? contactName, string? phone, string? email, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del proveedor es obligatorio.", nameof(name));
        }

        Name = name.Trim();
        ContactName = string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
    }
}
