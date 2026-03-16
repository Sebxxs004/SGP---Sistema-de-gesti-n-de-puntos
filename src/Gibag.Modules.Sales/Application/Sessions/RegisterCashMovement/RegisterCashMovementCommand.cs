using Gibag.Modules.Sales.Domain;
using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Sales.Application.Sessions.RegisterCashMovement;

public record RegisterCashMovementCommand(
    Guid BranchId,
    CashMovementType Type,
    decimal Amount,
    string Reason
) : IRequest<Result<Guid>>;
