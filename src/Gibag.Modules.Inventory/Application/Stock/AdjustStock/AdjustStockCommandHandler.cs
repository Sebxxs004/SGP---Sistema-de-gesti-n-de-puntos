using Gibag.Modules.Inventory.Domain;
using Gibag.Modules.Inventory.Infrastructure;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Inventory.Application.Stock.AdjustStock;

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result<Guid>>
{
    private readonly InventoryDbContext _dbContext;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUser _currentUser;

    public AdjustStockCommandHandler(InventoryDbContext dbContext, ITenantService tenantService, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino para esta operación.");
        }

        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.IsActive, cancellationToken);

        if (product == null)
        {
            return Result<Guid>.Failure("Inventory.ProductNotFound", "El producto no existe o está inactivo.");
        }

        var branchStock = await _dbContext.BranchStocks
            .FirstOrDefaultAsync(bs => bs.BranchId == request.BranchId && bs.ProductId == request.ProductId, cancellationToken);

        var currentQty = branchStock?.Quantity ?? 0m;
        var newQty = currentQty + request.QuantityDelta;
        if (newQty < 0)
        {
            return Result<Guid>.Failure("Inventory.StockNegative", "El ajuste no puede dejar el stock en negativo.");
        }

        if (branchStock == null)
        {
            branchStock = new BranchStock(tenantId.Value, request.BranchId, request.ProductId, newQty, minStockLevel: 0m);
            await _dbContext.BranchStocks.AddAsync(branchStock, cancellationToken);
        }
        else
        {
            _dbContext.Entry(branchStock).Property("Quantity").CurrentValue = newQty;
        }

        var userId = _currentUser.Id ?? Guid.Empty;
        var movement = new StockMovement(
            tenantId.Value,
            request.BranchId,
            request.ProductId,
            userId,
            MovementType.Adjustment,
            request.QuantityDelta,
            request.Reason.Trim());

        await _dbContext.StockMovements.AddAsync(movement, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(movement.Id);
    }
}
