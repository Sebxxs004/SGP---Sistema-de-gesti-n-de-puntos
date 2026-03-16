using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public class CashRegisterSession : TenantEntityBase
{
    public Guid BranchId { get; private set; }
    public Guid UserId { get; private set; } // The cashier who opened it
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public decimal InitialBalance { get; private set; }
    public decimal? FinalBalanceEncounted { get; private set; }
    public decimal? FinalBalanceExpected { get; private set; }
    public bool IsOpen { get; private set; }

    // Navigation
    public virtual ICollection<Sale> Sales { get; private set; }
    public virtual ICollection<CashMovement> CashMovements { get; private set; }

    private CashRegisterSession() 
    {
        Sales = new List<Sale>();
        CashMovements = new List<CashMovement>();
    }

    public CashRegisterSession(Guid tenantId, Guid branchId, Guid userId, decimal initialAmount) 
        : base(tenantId)
    {
        Id = Guid.NewGuid();
        BranchId = branchId;
        UserId = userId;
        OpenedAt = DateTimeOffset.UtcNow;
        InitialBalance = initialAmount;
        IsOpen = true;
        Sales = new List<Sale>();
        CashMovements = new List<CashMovement>();
    }

    public void Close(decimal finalBalanceEncounted, decimal finalBalanceExpected)
    {
        if (!IsOpen) throw new InvalidOperationException("Session is already closed.");

        ClosedAt = DateTimeOffset.UtcNow;
        FinalBalanceEncounted = finalBalanceEncounted;
        FinalBalanceExpected = finalBalanceExpected;
        IsOpen = false;
    }
}
