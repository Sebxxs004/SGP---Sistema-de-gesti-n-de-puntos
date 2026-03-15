using Gibag.Shared.Domain;

namespace Gibag.Modules.Core.Domain;

public class User : TenantEntityBase
{
    public Guid RoleId { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }

    // Navigation properties
    public virtual Tenant? Tenant { get; private set; }
    public virtual Role? Role { get; private set; }
    public virtual ICollection<UserBranch> UserBranches { get; private set; }

    private User() 
    {
        Email = string.Empty;
        PasswordHash = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        UserBranches = new List<UserBranch>();
    }

    public User(Guid tenantId, Guid roleId, string email, string passwordHash, string firstName, string lastName) : base(tenantId)
    {
        Id = Guid.NewGuid();
        RoleId = roleId;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        UserBranches = new List<UserBranch>();
    }
}
