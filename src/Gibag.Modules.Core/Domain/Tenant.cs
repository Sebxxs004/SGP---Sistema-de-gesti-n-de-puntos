using Gibag.Shared.Domain;

namespace Gibag.Modules.Core.Domain;

public class Tenant : EntityBase
{
    public string Name { get; private set; }
    public string TaxId { get; private set; }
    public string? ThankYouMessage { get; private set; }
    public decimal TaxPercentage { get; private set; }
    public string CurrencySymbol { get; private set; }
    public bool IsActive { get; private set; }
    public string SubscriptionPlan { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    
    // Navigation properties
    public virtual ICollection<Branch> Branches { get; private set; }
    public virtual ICollection<Role> Roles { get; private set; }
    public virtual ICollection<User> Users { get; private set; }

    private Tenant() 
    {
        Name = string.Empty;
        TaxId = string.Empty;
        ThankYouMessage = null;
        CurrencySymbol = "$";
        SubscriptionPlan = string.Empty;
        Branches = new List<Branch>();
        Roles = new List<Role>();
        Users = new List<User>();
    }

    public Tenant(string name, string taxId, string subscriptionPlan)
    {
        Id = Guid.NewGuid();
        Name = name;
        TaxId = taxId;
        ThankYouMessage = "Gracias por su compra";
        TaxPercentage = 16m;
        CurrencySymbol = "$";
        IsActive = true;
        SubscriptionPlan = subscriptionPlan;
        CreatedAt = DateTimeOffset.UtcNow;
        Branches = new List<Branch>();
        Roles = new List<Role>();
        Users = new List<User>();
    }

    public void UpdateThankYouMessage(string? message)
    {
        ThankYouMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
    }

    public void UpdateFinancialSettings(decimal taxPercentage, string? currencySymbol)
    {
        TaxPercentage = taxPercentage < 0 ? 0 : taxPercentage;
        CurrencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? "$" : currencySymbol.Trim();
    }
}
