using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public enum CashMovementType
{
    CashIn,
    CashOut
}

public class CashMovement : TenantEntityBase
{
    public Guid SessionId { get; private set; }
    public CashMovementType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public virtual CashRegisterSession? Session { get; private set; }

    private CashMovement()
    {
        Reason = string.Empty;
    }

    public CashMovement(Guid tenantId, Guid sessionId, CashMovementType type, decimal amount, string reason)
        : base(tenantId)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("SessionId es requerido.", nameof(sessionId));
        if (amount <= 0m) throw new ArgumentException("El monto debe ser mayor a cero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("El motivo es requerido.", nameof(reason));

        Id = Guid.NewGuid();
        SessionId = sessionId;
        Type = type;
        Amount = amount;
        Reason = reason.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
