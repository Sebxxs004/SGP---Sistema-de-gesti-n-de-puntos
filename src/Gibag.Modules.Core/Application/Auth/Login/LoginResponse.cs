namespace Gibag.Modules.Core.Application.Auth.Login;

public sealed record LoginBranchDto(Guid Id, string Name, bool IsPrimary);

public sealed record LoginResponse(
    string Token,
    Guid UserId,
    string Email,
    Guid? DefaultBranchId,
    IReadOnlyList<LoginBranchDto> Branches
);
