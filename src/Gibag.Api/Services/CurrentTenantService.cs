using Gibag.Shared.Interfaces;

namespace Gibag.Api.Services;

// This service is a transient/scoped implementation of the Shared Interfaces
public class CurrentTenantService : ITenantService, ICurrentUser
{
    public Guid? CurrentTenantId { get; private set; }
    
    public Guid? Id { get; set; }
    public Guid? TenantId => CurrentTenantId;
    public Guid? BranchId { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }

    public void SetCurrentTenantId(Guid tenantId)
    {
        CurrentTenantId = tenantId;
    }
}
