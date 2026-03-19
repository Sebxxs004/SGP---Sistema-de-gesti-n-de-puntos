using Gibag.Shared.Domain;

namespace Gibag.Modules.Sales.Domain;

public enum PaymentMethod
{
    Cash,
    CreditCard,
    DebitCard,
    Transfer,
    Other,
    Credit
}

public class Payment : TenantEntityBase
{
    public Guid SaleId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    
    public virtual Sale? Sale { get; private set; }

    private Payment() {}

    public Payment(Guid id, Guid tenantId, Guid saleId, decimal amount, PaymentMethod method)
        : base(tenantId)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        SaleId = saleId;
        Amount = amount;
        Method = method;
    }
}
