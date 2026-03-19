using Gibag.Shared.Domain;

namespace Gibag.Modules.Core.Domain;

public class Customer : TenantEntityBase
{
    public string Name { get; private set; }
    public string? DocumentNumber { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }

    private Customer()
    {
        Name = string.Empty;
    }

    public Customer(Guid tenantId, string name, string? documentNumber, string? email, string? phone, string? address)
        : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        DocumentNumber = NormalizeOptional(documentNumber);
        Email = NormalizeOptional(email);
        Phone = NormalizeOptional(phone);
        Address = NormalizeOptional(address);
        IsActive = true;
    }

    public void Update(string name, string? documentNumber, string? email, string? phone, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(name));
        }

        Name = name.Trim();
        DocumentNumber = NormalizeOptional(documentNumber);
        Email = NormalizeOptional(email);
        Phone = NormalizeOptional(phone);
        Address = NormalizeOptional(address);
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
