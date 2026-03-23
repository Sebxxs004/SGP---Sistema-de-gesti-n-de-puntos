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

        if (request.CustomerId.HasValue)
        {
            var customerExists = await _coreDbContext.Customers
                .AsNoTracking()
                .AnyAsync(c => c.Id == request.CustomerId.Value && c.IsActive, cancellationToken);

            if (!customerExists)
                return Result<Guid>.Failure("Customer.NotFound", "El cliente seleccionado no existe o está inactivo.");
        }

        var hasCreditPayment = request.Payments.Any(p => p.Method == PaymentMethod.Credit);
        if (hasCreditPayment && (!request.CustomerId.HasValue || request.CustomerId.Value == Guid.Empty))
            return Result<Guid>.Failure("Sales.CreditRequiresCustomer", "Para vender a crédito debes seleccionar un cliente.");

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

        var productIds = request.Details
            .Select(d => d.ProductId)
            .Distinct()
            .ToList();

        var productPricing = await _inventoryService.GetProductPricingAsync(productIds, cancellationToken);

        foreach (var detailDto in request.Details)
        {
            var pricing = productPricing.TryGetValue(detailDto.ProductId, out var resolvedPricing)
                ? resolvedPricing
                : new InventoryProductPricing(detailDto.UnitPrice, 0m);

            sale.AddDetail(new SaleDetail(
                detailDto.Id,
                tenantId.Value,
                sale.Id,
                detailDto.ProductId,
                detailDto.Quantity,
                detailDto.UnitPrice,
                detailDto.DiscountAmount,
                pricing.UnitCost,
                pricing.TaxRate,
                0m
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
        var taxCalculated = ApplyItemTaxBreakdown(sale, request.Discount);
        var totalCalculated = subTotalAfterDiscount + taxCalculated;

        sale.UpdateFinancials(subTotalCalculated, taxCalculated, totalCalculated, request.Discount);
        sale.AssignCustomer(request.CustomerId);
        sale.MarkAsCompleted();

        UpsertAccountReceivableFromPayments(sale, request.Payments);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(sale.Id);
    }

    private static decimal ApplyItemTaxBreakdown(Sale sale, decimal saleDiscount)
    {
        var details = sale.Details.ToList();
        if (details.Count == 0)
        {
            return 0m;
        }

        var normalizedDiscount = Math.Max(saleDiscount, 0m);
        var totalBase = details.Sum(d => Math.Max((d.UnitPrice - d.DiscountAmount) * d.Quantity, 0m));
        var remainingDiscount = normalizedDiscount;
        var accumulatedTax = 0m;

        for (var i = 0; i < details.Count; i++)
        {
            var detail = details[i];
            var lineBase = Math.Max((detail.UnitPrice - detail.DiscountAmount) * detail.Quantity, 0m);

            decimal allocatedDiscount;
            if (i == details.Count - 1)
            {
                allocatedDiscount = Math.Min(remainingDiscount, lineBase);
            }
            else if (totalBase <= 0m)
            {
                allocatedDiscount = 0m;
            }
            else
            {
                allocatedDiscount = Math.Round(normalizedDiscount * (lineBase / totalBase), 2, MidpointRounding.AwayFromZero);
                allocatedDiscount = Math.Min(allocatedDiscount, lineBase);
                allocatedDiscount = Math.Min(allocatedDiscount, remainingDiscount);
            }

            remainingDiscount = Math.Max(remainingDiscount - allocatedDiscount, 0m);

            var effectiveTaxRate = detail.TaxRate < 0m ? 0m : detail.TaxRate;
            var taxableBase = Math.Max(lineBase - allocatedDiscount, 0m);
            var taxAmount = Math.Round(taxableBase * (effectiveTaxRate / 100m), 2, MidpointRounding.AwayFromZero);

            detail.SetTaxBreakdown(effectiveTaxRate, taxAmount);
            accumulatedTax += taxAmount;
        }

        return Math.Round(accumulatedTax, 2, MidpointRounding.AwayFromZero);
    }

    private void UpsertAccountReceivableFromPayments(Sale sale, List<CreateSalePaymentDto> payments)
    {
        var hasCredit = payments.Any(p => p.Method == PaymentMethod.Credit);
        if (!hasCredit || !sale.CustomerId.HasValue)
        {
            return;
        }

        var nonCreditPaid = payments
            .Where(p => p.Method != PaymentMethod.Credit)
            .Sum(p => p.Amount);

        var dueDate = sale.CreatedAt.AddDays(30);

        var receivable = _dbContext.AccountReceivables
            .FirstOrDefault(ar => ar.SaleId == sale.Id);

        if (receivable == null)
        {
            receivable = new AccountReceivable(
                sale.TenantId,
                sale.CustomerId.Value,
                sale.Id,
                sale.Total,
                nonCreditPaid,
                dueDate);

            _dbContext.AccountReceivables.Add(receivable);
            return;
        }

        receivable.SyncFromSale(sale.Total, nonCreditPaid, dueDate);
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