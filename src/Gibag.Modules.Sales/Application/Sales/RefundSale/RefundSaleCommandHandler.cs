using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Modules.Sales.Application.Interfaces;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Sales.Application.Sales.RefundSale;

public class RefundSaleCommandHandler : IRequestHandler<RefundSaleCommand, Result<Guid>>
{
    private readonly SalesDbContext _dbContext;
    private readonly IInventoryService _inventoryService;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUser _currentUser;

    public RefundSaleCommandHandler(
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

    public async Task<Result<Guid>> Handle(RefundSaleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino.");

        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
            return Result<Guid>.Failure("Branch.Required", "Debes enviar X-Branch-Id para procesar devoluciones.");

        // Retrieve the sale with all details and payments
        var sale = await _dbContext.Sales
            .Include(s => s.Details)
            .Include(s => s.Payments)
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.Id == request.SaleId && s.BranchId == currentBranchId.Value, cancellationToken);

        if (sale == null)
            return Result<Guid>.Failure("Sales.NotFound", "La venta no existe o no pertenece a tu sucursal.");

        if (sale.Status != SaleStatus.Completed)
            return Result<Guid>.Failure("Sales.InvalidStatus", "Solo se pueden devolver ventas completadas.");

        if (sale.IsRefunded)
            return Result<Guid>.Failure("Sales.AlreadyRefunded", "Esta venta ya ha sido reembolsada.");

        // Validate current open cash session for the cashier
        var activeSession = await _dbContext.CashRegisterSessions
            .FirstOrDefaultAsync(
                s => s.UserId == _currentUser.Id && s.BranchId == currentBranchId.Value && s.IsOpen,
                cancellationToken);

        if (activeSession == null)
            return Result<Guid>.Failure("Sales.NoActiveSession", "No existe una sesión de caja activa para procesar devoluciones.");

        if (sale.SessionId != activeSession.Id)
            return Result<Guid>.Failure("Sales.SessionMismatch", "Solo puedes devolver tickets del turno de caja activo.");

        // Restore stock for all sold items
        foreach (var detail in sale.Details)
        {
            var incrementResult = await _inventoryService.IncrementStockAsync(
                currentBranchId.Value,
                detail.ProductId,
                detail.Quantity,
                $"Devolución: Venta {sale.Id}",
                cancellationToken);

            if (incrementResult.IsFailure)
            {
                return Result<Guid>.Failure(
                    incrementResult.ErrorCode ?? "Sales.StockError",
                    incrementResult.ErrorMessage ?? "Error restaurando inventario");
            }
        }

        // Mark sale as refunded
        sale.MarkAsRefunded();

        // Record negative payment (refund) to adjust cash register balance
        var refundPayment = new Payment(
            Guid.NewGuid(),
            tenantId.Value,
            sale.Id,
            -sale.Total, // Negative amount represents outflow
            PaymentMethod.Cash // Assume cash refund (could be configurable)
        );
        sale.AddPayment(refundPayment);

        // Save changes
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(sale.Id);
    }
}
