using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public class Sale : TenantEntityBase
{
    public Guid SessionId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Total { get; private set; }

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
                decimal subTotal, decimal tax, decimal total, DateTimeOffset? createdAt = null) 
        : base(tenantId)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        SessionId = sessionId;
        BranchId = branchId;
        UserId = userId;
        SubTotal = subTotal;
        Tax = tax;
        Total = total;
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
}
