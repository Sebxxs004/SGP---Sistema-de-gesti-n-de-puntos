namespace Gibag.Shared.Interfaces;

public interface ITenantService
{
    Guid? CurrentTenantId { get; }
    void SetCurrentTenantId(Guid tenantId);
}
