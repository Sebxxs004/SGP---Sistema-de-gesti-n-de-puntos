using Gibag.Modules.Inventory.Domain;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Inventory.Application.Products.CreateProduct;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public CreateProductCommandHandler(InventoryDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino para esta operación.");
        }

        bool categoryExists = await _dbContext.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (!categoryExists)
        {
            return Result<Guid>.Failure("Inventory.CategoryNotFound", "La categoría especificada no existe.");
        }

        // Global Query Filter automatically scopes this AnyAsync query to the CurrentTenantId
        bool skuExists = await _dbContext.Products
            .AnyAsync(p => p.SKU == request.SKU, cancellationToken);
            
        if (skuExists)
        {
            return Result<Guid>.Failure("Inventory.SKUExists", $"El SKU '{request.SKU}' ya existe en el inventario.");
        }

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            bool barcodeExists = await _dbContext.Products
                .AnyAsync(p => p.Barcode == request.Barcode, cancellationToken);

            if (barcodeExists)
            {
                return Result<Guid>.Failure("Inventory.BarcodeExists", $"El código de barras '{request.Barcode}' ya existe.");
            }
        }

        var product = new Product(
            tenantId.Value,
            request.CategoryId,
            request.Name,
            request.SKU,
            request.Barcode,
            request.BasePrice,
            request.Cost
        );

        await _dbContext.Products.AddAsync(product, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(product.Id);
    }
}
