using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public enum SaleStatus
{
    Pending,
    Completed,
    Refunded
}

public class Sale : TenantEntityBase
{
    public Guid SessionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Total { get; private set; }

    public decimal Discount { get; private set; } // Total discount applied to sale
    public bool IsRefunded { get; private set; } // For audit trail: marks refunded sales
    public SaleStatus Status { get; private set; }

    // Navigation
    public virtual CashRegisterSession? Session { get; private set; }
    public virtual ICollection<SaleDetail> Details { get; private set; }
    public virtual ICollection<Payment> Payments { get; private set; }
    private Sale() 
    {
        Details = new List<SaleDetail>();
        Payments = new List<Payment>();
    }

    // El parámetro ID explícito permite que el Frontend en PWA Offline-First pueda generar 
    // su propio UUID y lo envíe en la sincronización, evitando duplicados.
    public Sale(Guid id, Guid tenantId, Guid sessionId, Guid branchId, Guid userId,
                decimal subTotal, decimal tax, decimal total, decimal discount = 0m,
                SaleStatus status = SaleStatus.Completed, DateTimeOffset? createdAt = null)
        : base(tenantId)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        SessionId = sessionId;
        BranchId = branchId;
        UserId = userId;
        SubTotal = subTotal;
        Tax = tax;
        Total = total;
        Discount = discount;
        Status = status;
        IsRefunded = status == SaleStatus.Refunded;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow; // Permite fecha original offline
        
        Details = new List<SaleDetail>();
        Payments = new List<Payment>();
    }

    public void AddDetail(SaleDetail detail)
    {
        Details.Add(detail);
    }

    public void AddPayment(Payment payment)
    {
        Payments.Add(payment);
    }

    public void UpdateTotals(decimal subTotal, decimal tax, decimal total)
    {
        SubTotal = subTotal;
        Tax = tax;
        Total = total;
    }

    public void UpdateFinancials(decimal subTotal, decimal tax, decimal total, decimal discount)
    {
        if (discount < 0)
            throw new ArgumentException("El descuento no puede ser negativo.");
        if (discount > subTotal)
            throw new ArgumentException("El descuento no puede superar el subtotal.");

        SubTotal = subTotal;
        Tax = tax;
        Total = total;
        Discount = discount;
    }

    public void ApplyDiscount(decimal discountAmount)
    {
        if (discountAmount < 0)
            throw new ArgumentException("El descuento no puede ser negativo.");
        if (discountAmount > SubTotal)
            throw new ArgumentException("El descuento no puede superar el subtotal.");
        Discount = discountAmount;
    }

    public void MarkAsPending()
    {
        Status = SaleStatus.Pending;
        IsRefunded = false;
    }

    public void MarkAsCompleted()
    {
        Status = SaleStatus.Completed;
        IsRefunded = false;
    }

    public void MarkAsRefunded()
    {
        Status = SaleStatus.Refunded;
        IsRefunded = true;
    }
}
