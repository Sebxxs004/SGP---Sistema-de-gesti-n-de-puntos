using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Inventory.Application.Products.CreateProduct;

public record CreateProductCommand(
    Guid CategoryId,
    string Name,
    string SKU,
    string? Barcode,
    decimal BasePrice,
    decimal Cost
) : IRequest<Result<Guid>>;
