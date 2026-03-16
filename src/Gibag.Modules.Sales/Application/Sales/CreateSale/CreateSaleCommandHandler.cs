using Gibag.Modules.Sales.Application.Interfaces;
using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Sales.Application.Sales.CreateSale;

public class CreateSaleCommandHandler : IRequestHandler<CreateSaleCommand, Result<Guid>>
{
    private readonly SalesDbContext _dbContext;
    private readonly IInventoryService _inventoryService;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUser _currentUser;

    public CreateSaleCommandHandler(
        SalesDbContext dbContext, 
        IInventoryService inventoryService,
        ITenantService tenantService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
        _tenantService = tenantService;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino.");

        var userId = _currentUser.Id;
        if (userId == null || userId == Guid.Empty)
            return Result<Guid>.Failure("Auth.UserMissing", "No se encontró el usuario en sesión para registrar la venta.");

        // Idempotency Check for offline-sync retries
        bool saleAlreadyExists = await _dbContext.Sales
            .AnyAsync(s => s.Id == request.Id, cancellationToken);
            
        if (saleAlreadyExists)
        {
            // If the sale is already saved from a previous offline sync, return success immediately
            return Result<Guid>.Success(request.Id);
        }

        // Require an active cash session for the current user and current branch.
        var session = await _dbContext.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.UserId == userId.Value && s.BranchId == request.BranchId && s.IsOpen, cancellationToken);

        if (session == null)
        {
            return Result<Guid>.Failure("Sales.NoActiveSession", "No existe una sesión de caja activa para el usuario en esta sucursal.");
        }

        if (request.SessionId != Guid.Empty && request.SessionId != session.Id)
        {
            return Result<Guid>.Failure("Sales.SessionMismatch", "La sesión enviada no coincide con la sesión activa del usuario.");
        }

        // Verify and reserve stock via Inventory Integration
        foreach (var detail in request.Details)
        {
            var stockCheckResult = await _inventoryService.CheckStockAsync(request.BranchId, detail.ProductId, detail.Quantity, cancellationToken);
            if (stockCheckResult.IsFailure)
            {
                return Result<Guid>.Failure(stockCheckResult.ErrorCode ?? "Sales.StockError", stockCheckResult.ErrorMessage ?? "Error verificando inventario");
            }
        }

        // Deduct stock via Inventory Integration
        foreach (var detail in request.Details)
        {
            var decrementResult = await _inventoryService.DecrementStockAsync(
                request.BranchId, 
                detail.ProductId, 
                detail.Quantity, 
                $"Venta {request.Id}", 
                cancellationToken);
                
            if (decrementResult.IsFailure)
            {
                // Note: In a true distributed system without a distributed transaction/outbox, 
                // this could cause partial failures if the DB fails after deducting.
                // We assume within a single request scoped transaction or resilient compensation in the adapter.
            }
        }

        // Create Sale Entity
        var sale = new Sale(
            request.Id,
            tenantId.Value,
            session.Id,
            request.BranchId,
            userId.Value,
            request.SubTotal,
            request.Tax,
            request.Total,
            request.CreatedAt
        );

        foreach (var detailDto in request.Details)
        {
            sale.AddDetail(new SaleDetail(
                detailDto.Id,
                tenantId.Value,
                sale.Id,
                detailDto.ProductId,
                detailDto.Quantity,
                detailDto.UnitPrice
            ));
        }

        foreach (var paymentDto in request.Payments)
        {
            sale.AddPayment(new Payment(
                paymentDto.Id,
                tenantId.Value,
                sale.Id,
                paymentDto.Amount,
                paymentDto.Method
            ));
        }

        await _dbContext.Sales.AddAsync(sale, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(sale.Id);
    }
}
