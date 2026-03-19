using Gibag.Modules.Core.Infrastructure;
using Gibag.Modules.Sales.Application.Interfaces;
using Gibag.Modules.Sales.Application.Sales.CreateSale;
using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Sales.Application.Sales.CompletePendingSale;

public class CompletePendingSaleCommandHandler : IRequestHandler<CompletePendingSaleCommand, Result<Guid>>
{
    private readonly SalesDbContext _dbContext;
    private readonly IInventoryService _inventoryService;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUser _currentUser;
    private readonly CoreDbContext _coreDbContext;

    public CompletePendingSaleCommandHandler(
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

    public async Task<Result<Guid>> Handle(CompletePendingSaleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino.");

        var userId = _currentUser.Id;
        if (userId == null || userId == Guid.Empty)
            return Result<Guid>.Failure("Auth.UserMissing", "No se encontró el usuario en sesión.");

        var tenantConfig = await _coreDbContext.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId.Value)
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
                return Result<Guid>.Failure("Customer.NotFound", "El cliente seleccionado no existe o está inactivo.");
        }

        var session = await _dbContext.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.UserId == userId.Value && s.BranchId == request.BranchId && s.IsOpen, cancellationToken);

        if (session == null)
            return Result<Guid>.Failure("Sales.NoActiveSession", "No existe una sesión de caja activa para completar la venta.");

        var sale = await _dbContext.Sales
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId && s.BranchId == request.BranchId, cancellationToken);

        if (sale == null)
            return Result<Guid>.Failure("Sales.NotFound", "La venta en espera no existe o no pertenece a esta sucursal.");

        if (sale.Status != SaleStatus.Pending)
            return Result<Guid>.Failure("Sales.InvalidStatus", "Solo se pueden completar ventas en espera.");

        if (sale.SessionId != session.Id)
            return Result<Guid>.Failure("Sales.SessionMismatch", "La venta en espera pertenece a una sesión distinta a la activa.");

        var stockAdjustment = await ReconcileStockAsync(sale, request.Details, request.BranchId, cancellationToken);
        if (stockAdjustment.IsFailure)
            return stockAdjustment;

        _dbContext.SaleDetails.RemoveRange(sale.Details.ToList());
        sale.Details.Clear();

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

        _dbContext.Payments.RemoveRange(sale.Payments.ToList());
        sale.Payments.Clear();

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
        sale.AssignCustomer(request.CustomerId);
        sale.MarkAsCompleted();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(sale.Id);
    }

    private async Task<Result<Guid>> ReconcileStockAsync(
        Sale sale,
        List<CreateSaleDetailDto> requestedDetails,
        Guid branchId,
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
                $"Venta {sale.Id}",
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
                $"Ajuste venta {sale.Id}",
                cancellationToken);

            if (incrementResult.IsFailure)
            {
                return Result<Guid>.Failure(
                    incrementResult.ErrorCode ?? "Sales.StockError",
                    incrementResult.ErrorMessage ?? "Error liberando inventario reservado");
            }
        }

        return Result<Guid>.Success(sale.Id);
    }
}