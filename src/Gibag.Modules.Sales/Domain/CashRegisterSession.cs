using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public class CashRegisterSession : TenantEntityBase
{
    public Guid BranchId { get; private set; }
    public Guid UserId { get; private set; } // The cashier who opened it
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public decimal InitialAmount { get; private set; }
    public decimal? FinalAmount { get; private set; }
    public bool IsOpen { get; private set; }

    // Navigation
    public virtual ICollection<Sale> Sales { get; private set; }

    private CashRegisterSession() 
    {
        Sales = new List<Sale>();
    }

    public CashRegisterSession(Guid tenantId, Guid branchId, Guid userId, decimal initialAmount) 
        : base(tenantId)
    {
        Id = Guid.NewGuid();
        BranchId = branchId;
        UserId = userId;
        StartedAt = DateTimeOffset.UtcNow;
        InitialAmount = initialAmount;
        IsOpen = true;
        Sales = new List<Sale>();
    }

    public void Close(decimal finalAmount)
    {
        if (!IsOpen) throw new InvalidOperationException("Session is already closed.");
        
        EndedAt = DateTimeOffset.UtcNow;
        FinalAmount = finalAmount;
        IsOpen = false;
    }
}
