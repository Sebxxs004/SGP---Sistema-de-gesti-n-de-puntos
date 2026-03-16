namespace Gibag.Shared.Interfaces;

public interface ITenantService
{
    Guid? CurrentTenantId { get; }
    Guid? CurrentBranchId { get; }
    void SetCurrentTenantId(Guid tenantId);
    void SetCurrentBranchId(Guid branchId);
}
