namespace Gibag.Shared.Domain;

public abstract class TenantEntityBase : EntityBase
{
    public Guid TenantId { get; protected set; }
    
    protected TenantEntityBase() {}
    
    protected TenantEntityBase(Guid tenantId)
    {
        TenantId = tenantId;
    }
}
