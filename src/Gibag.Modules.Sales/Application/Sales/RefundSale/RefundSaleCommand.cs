using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Sales.Application.Sales.RefundSale;

public record RefundSaleCommand(
    Guid SaleId,
    string Reason = "Devolución"
) : IRequest<Result<Guid>>;
