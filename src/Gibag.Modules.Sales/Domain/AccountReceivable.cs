using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public enum AccountReceivableStatus
{
    Pending,
    Partial,
    Paid
}

public class AccountReceivable : TenantEntityBase
{
    public Guid CustomerId { get; private set; }
    public Guid SaleId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal Balance { get; private set; }
    public DateTimeOffset DueDate { get; private set; }
    public AccountReceivableStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public virtual Sale? Sale { get; private set; }

    private AccountReceivable() { }

    public AccountReceivable(
        Guid tenantId,
        Guid customerId,
        Guid saleId,
        decimal totalAmount,
        decimal paidAmount,
        DateTimeOffset dueDate)
        : base(tenantId)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("El cliente es obligatorio para la cuenta por cobrar.", nameof(customerId));
        if (saleId == Guid.Empty)
            throw new ArgumentException("La venta es obligatoria para la cuenta por cobrar.", nameof(saleId));
        if (totalAmount < 0)
            throw new ArgumentException("El total de la deuda no puede ser negativo.", nameof(totalAmount));
        if (paidAmount < 0)
            throw new ArgumentException("El monto pagado no puede ser negativo.", nameof(paidAmount));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        SaleId = saleId;
        TotalAmount = totalAmount;
        PaidAmount = Math.Min(paidAmount, totalAmount);
        Balance = Math.Max(totalAmount - PaidAmount, 0m);
        DueDate = dueDate;
        CreatedAt = DateTimeOffset.UtcNow;
        Status = ResolveStatus(Balance, PaidAmount);
    }

    public void SyncFromSale(decimal totalAmount, decimal paidAmount, DateTimeOffset dueDate)
    {
        if (totalAmount < 0)
            throw new ArgumentException("El total de la deuda no puede ser negativo.", nameof(totalAmount));
        if (paidAmount < 0)
            throw new ArgumentException("El monto pagado no puede ser negativo.", nameof(paidAmount));

        TotalAmount = totalAmount;
        PaidAmount = Math.Min(paidAmount, totalAmount);
        Balance = Math.Max(totalAmount - PaidAmount, 0m);
        DueDate = dueDate;
        Status = ResolveStatus(Balance, PaidAmount);
    }

    public void RegisterPayment(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("El abono debe ser mayor a cero.", nameof(amount));
        if (Status == AccountReceivableStatus.Paid || Balance <= 0)
            throw new InvalidOperationException("La deuda ya está saldada.");
        if (amount > Balance)
            throw new InvalidOperationException("El abono supera el saldo pendiente.");

        PaidAmount += amount;
        Balance = Math.Max(TotalAmount - PaidAmount, 0m);
        Status = ResolveStatus(Balance, PaidAmount);
    }

    private static AccountReceivableStatus ResolveStatus(decimal balance, decimal paidAmount)
    {
        if (balance <= 0m)
            return AccountReceivableStatus.Paid;

        return paidAmount > 0m
            ? AccountReceivableStatus.Partial
            : AccountReceivableStatus.Pending;
    }
}
