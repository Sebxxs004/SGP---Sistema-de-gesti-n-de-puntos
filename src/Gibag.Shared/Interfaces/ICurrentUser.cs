namespace Gibag.Shared.Interfaces;

public interface ICurrentUser
{
    Guid? Id { get; }
    Guid? TenantId { get; }
    Guid? BranchId { get; }
    string? Email { get; }
    string? Role { get; }
}
