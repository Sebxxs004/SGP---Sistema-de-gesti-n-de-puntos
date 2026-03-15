using Gibag.Shared.Domain;

namespace Gibag.Modules.Core.Domain;

public class UserBranch : TenantEntityBase
{
    public Guid UserId { get; private set; }
    public Guid BranchId { get; private set; }
    public bool IsPrimary { get; private set; }

    // Navigation properties
    public virtual User? User { get; private set; }
    public virtual Branch? Branch { get; private set; }
    public virtual Tenant? Tenant { get; private set; }

    private UserBranch() {}

    public UserBranch(Guid tenantId, Guid userId, Guid branchId, bool isPrimary) : base(tenantId)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        BranchId = branchId;
        IsPrimary = isPrimary;
    }
}
