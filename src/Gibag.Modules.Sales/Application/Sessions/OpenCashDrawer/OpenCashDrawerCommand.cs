using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;

public record OpenCashDrawerCommand(
    Guid BranchId,
    decimal InitialAmount
) : IRequest<Result<Guid>>;
