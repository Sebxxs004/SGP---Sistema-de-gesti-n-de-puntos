using Gibag.Shared.Domain;

namespace Gibag.Modules.Core.Domain;

public class Branch : TenantEntityBase
{
    public string Name { get; private set; }
    public string Address { get; private set; }
    public string? Phone { get; private set; }
    public string Timezone { get; private set; }
    public bool IsActive { get; private set; }
    
    // Navigation property
    public virtual Tenant? Tenant { get; private set; }

    private Branch() 
    {
        Name = string.Empty;
        Address = string.Empty;
        Phone = null;
        Timezone = string.Empty;
    }

    public Branch(Guid tenantId, string name, string address, string timezone, string? phone = null) : base(tenantId)
    {
        Id = Guid.NewGuid();
        Name = name;
        Address = address;
        Phone = phone;
        Timezone = timezone;
        IsActive = true;
    }

    public void Update(string name, string address, string timezone, string? phone)
    {
        Name = name;
        Address = address;
        Timezone = timezone;
        Phone = phone;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
