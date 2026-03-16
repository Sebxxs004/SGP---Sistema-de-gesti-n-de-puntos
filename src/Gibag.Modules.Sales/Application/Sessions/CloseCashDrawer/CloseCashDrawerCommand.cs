using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Sales.Application.Sessions.CloseCashDrawer;

public record CloseCashDrawerCommand(
    Guid BranchId,
    decimal FinalBalanceEncounted
) : IRequest<Result<CloseCashDrawerResultDto>>;

public record CloseCashDrawerResultDto(
    Guid SessionId,
    decimal InitialBalance,
    decimal SalesTotal,
    decimal FinalBalanceExpected,
    decimal FinalBalanceEncounted,
    decimal Difference
);
