using Gibag.Modules.Sales.Application.Sales.CreateSale;
using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Sales.Application.Sales.CompletePendingSale;

public record CompletePendingSaleCommand(
    Guid SaleId,
    Guid BranchId,
    decimal Discount,
    List<CreateSaleDetailDto> Details,
    List<CreateSalePaymentDto> Payments
) : IRequest<Result<Guid>>;