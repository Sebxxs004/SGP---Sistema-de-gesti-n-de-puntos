using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Inventory.Application.Stock.AdjustStock;

public record AdjustStockCommand(
    Guid BranchId,
    Guid ProductId,
    decimal QuantityDelta,
    string Reason
) : IRequest<Result<Guid>>;
