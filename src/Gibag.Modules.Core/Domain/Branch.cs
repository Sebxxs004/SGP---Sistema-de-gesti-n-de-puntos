using Gibag.Shared.Domain;

namespace Gibag.Modules.Core.Domain;

public class Branch : TenantEntityBase
{
    public string Name { get; private set; }
    public string Address { get; private set; }
    public string Timezone { get; private set; }
    public bool IsActive { get; private set; }
    
    // Navigation property
    public virtual Tenant? Tenant { get; private set; }

    private Branch() 
    {
        Name = string.Empty;
        Address = string.Empty;
        Timezone = string.Empty;
    }

    public Branch(Guid tenantId, string name, string address, string timezone) : base(tenantId)
    {
        Id = Guid.NewGuid();
        Name = name;
        Address = address;
        Timezone = timezone;
        IsActive = true;
    }
}
