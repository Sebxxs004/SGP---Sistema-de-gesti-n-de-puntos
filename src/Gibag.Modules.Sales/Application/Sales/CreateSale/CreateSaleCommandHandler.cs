using Gibag.Modules.Sales.Application.Interfaces;
using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Modules.Core.Infrastructure;
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
    private readonly CoreDbContext _coreDbContext;

    public CreateSaleCommandHandler(
        SalesDbContext dbContext, 
        IInventoryService inventoryService,
        ITenantService tenantService,
        ICurrentUser currentUser,
        CoreDbContext coreDbContext)
    {
        _dbContext = dbContext;
        _inventoryService = inventoryService;
        _tenantService = tenantService;
        _currentUser = currentUser;
        _coreDbContext = coreDbContext;
    }

    public async Task<Result<Guid>> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        var targetStatus = request.Status ?? SaleStatus.Completed;

        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino.");

        var userId = _currentUser.Id;
        if (userId == null || userId == Guid.Empty)
            return Result<Guid>.Failure("Auth.UserMissing", "No se encontró el usuario en sesión para registrar la venta.");

        var tenantConfig = await _coreDbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId.Value)
            .Select(t => new
            {
                t.TaxPercentage
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantConfig == null)
            return Result<Guid>.Failure("Tenant.NotFound", "No se encontró la configuración de la empresa.");

        if (request.CustomerId.HasValue)
        {
            var customerExists = await _coreDbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CustomerId.Value && c.IsActive, cancellationToken);

            if (!customerExists)
            {
                return Result<Guid>.Failure("Customer.NotFound", "El cliente seleccionado no existe o está inactivo.");
            }
        }

        var existingSale = await _dbContext.Sales
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (existingSale != null)
        {
            if (existingSale.Status == SaleStatus.Pending && targetStatus == SaleStatus.Pending)
            {
                return await UpdatePendingSaleAsync(existingSale, request, tenantId.Value, cancellationToken);
            }

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
            targetStatus == SaleStatus.Pending ? $"Venta en espera {request.Id}" : $"Venta {request.Id}", 
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
            request.CustomerId,
            request.Discount,
            targetStatus,
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
                detailDto.UnitPrice,
                detailDto.DiscountAmount
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

        var subTotalCalculated = sale.Details.Sum(d => d.SubTotal);
        var subTotalAfterDiscount = Math.Max(subTotalCalculated - request.Discount, 0m);
        var taxCalculated = Math.Round(subTotalAfterDiscount * (tenantConfig.TaxPercentage / 100m), 2, MidpointRounding.AwayFromZero);
        var totalCalculated = subTotalAfterDiscount + taxCalculated;

        sale.UpdateFinancials(subTotalCalculated, taxCalculated, totalCalculated, request.Discount);

        if (targetStatus == SaleStatus.Pending)
        {
            sale.MarkAsPending();
        }
        else
        {
            sale.MarkAsCompleted();
        }

        await _dbContext.Sales.AddAsync(sale, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(sale.Id);
    }

    private async Task<Result<Guid>> UpdatePendingSaleAsync(Sale sale, CreateSaleCommand request, Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantConfig = await _coreDbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.TaxPercentage })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantConfig == null)
            return Result<Guid>.Failure("Tenant.NotFound", "No se encontró la configuración de la empresa.");

        if (request.CustomerId.HasValue)
        {
            var customerExists = await _coreDbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CustomerId.Value && c.IsActive, cancellationToken);

            if (!customerExists)
            {
                return Result<Guid>.Failure("Customer.NotFound", "El cliente seleccionado no existe o está inactivo.");
            }
        }

        var userId = _currentUser.Id;
        if (userId == null || userId == Guid.Empty)
            return Result<Guid>.Failure("Auth.UserMissing", "No se encontró el usuario en sesión para actualizar la venta en espera.");

        var session = await _dbContext.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.UserId == userId.Value && s.BranchId == request.BranchId && s.IsOpen, cancellationToken);

        if (session == null)
            return Result<Guid>.Failure("Sales.NoActiveSession", "No existe una sesión de caja activa para el usuario en esta sucursal.");

        if (sale.SessionId != session.Id)
            return Result<Guid>.Failure("Sales.SessionMismatch", "La venta en espera pertenece a una sesión distinta a la activa.");

        var stockAdjustment = await ReconcileStockAsync(sale, request.Details, request.BranchId, sale.Id, cancellationToken);
        if (stockAdjustment.IsFailure)
            return stockAdjustment;

        _dbContext.SaleDetails.RemoveRange(sale.Details.ToList());
        sale.Details.Clear();

        foreach (var detailDto in request.Details)
        {
            sale.AddDetail(new SaleDetail(
                detailDto.Id,
                tenantId,
                sale.Id,
                detailDto.ProductId,
                detailDto.Quantity,
                detailDto.UnitPrice,
                detailDto.DiscountAmount
            ));
        }

        var subTotalCalculated = sale.Details.Sum(d => d.SubTotal);
        var subTotalAfterDiscount = Math.Max(subTotalCalculated - request.Discount, 0m);
        var taxCalculated = Math.Round(subTotalAfterDiscount * (tenantConfig.TaxPercentage / 100m), 2, MidpointRounding.AwayFromZero);
        var totalCalculated = subTotalAfterDiscount + taxCalculated;

        sale.UpdateFinancials(subTotalCalculated, taxCalculated, totalCalculated, request.Discount);
        sale.AssignCustomer(request.CustomerId);
        sale.MarkAsPending();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(sale.Id);
    }

    private async Task<Result<Guid>> ReconcileStockAsync(
        Sale sale,
        List<CreateSaleDetailDto> requestedDetails,
        Guid branchId,
        Guid saleId,
        CancellationToken cancellationToken)
    {
        var existingByProduct = sale.Details
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var requestedByProduct = requestedDetails
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var entry in requestedByProduct)
        {
            var previousQuantity = existingByProduct.GetValueOrDefault(entry.Key, 0m);
            var delta = entry.Value - previousQuantity;
            if (delta <= 0m)
                continue;

            var stockCheckResult = await _inventoryService.CheckStockAsync(branchId, entry.Key, delta, cancellationToken);
            if (stockCheckResult.IsFailure)
            {
                return Result<Guid>.Failure(
                    stockCheckResult.ErrorCode ?? "Sales.StockError",
                    stockCheckResult.ErrorMessage ?? "Error verificando inventario");
            }
        }

        foreach (var entry in requestedByProduct)
        {
            var previousQuantity = existingByProduct.GetValueOrDefault(entry.Key, 0m);
            var delta = entry.Value - previousQuantity;
            if (delta <= 0m)
                continue;

            var decrementResult = await _inventoryService.DecrementStockAsync(
                branchId,
                entry.Key,
                delta,
                $"Venta en espera {saleId}",
                cancellationToken);

            if (decrementResult.IsFailure)
            {
                return Result<Guid>.Failure(
                    decrementResult.ErrorCode ?? "Sales.StockError",
                    decrementResult.ErrorMessage ?? "Error actualizando inventario");
            }
        }

        foreach (var entry in existingByProduct)
        {
            var requestedQuantity = requestedByProduct.GetValueOrDefault(entry.Key, 0m);
            var delta = entry.Value - requestedQuantity;
            if (delta <= 0m)
                continue;

            var incrementResult = await _inventoryService.IncrementStockAsync(
                branchId,
                entry.Key,
                delta,
                $"Ajuste venta en espera {saleId}",
                cancellationToken);

            if (incrementResult.IsFailure)
            {
                return Result<Guid>.Failure(
                    incrementResult.ErrorCode ?? "Sales.StockError",
                    incrementResult.ErrorMessage ?? "Error devolviendo inventario reservado");
            }
        }

        return Result<Guid>.Success(sale.Id);
    }
}
