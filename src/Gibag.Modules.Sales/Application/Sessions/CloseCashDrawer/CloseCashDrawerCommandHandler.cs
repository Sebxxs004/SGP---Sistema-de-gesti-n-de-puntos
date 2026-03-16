using Gibag.Modules.Sales.Infrastructure;
using Gibag.Modules.Sales.Domain;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Sales.Application.Sessions.CloseCashDrawer;

public class CloseCashDrawerCommandHandler : IRequestHandler<CloseCashDrawerCommand, Result<CloseCashDrawerResultDto>>
{
    private readonly SalesDbContext _dbContext;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUser _currentUser;

    public CloseCashDrawerCommandHandler(SalesDbContext dbContext, ITenantService tenantService, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
        _currentUser = currentUser;
    }

    public async Task<Result<CloseCashDrawerResultDto>> Handle(CloseCashDrawerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
            return Result<CloseCashDrawerResultDto>.Failure("Auth.TenantMissing", "No se encontró el inquilino.");

        var userId = _currentUser.Id;
        if (userId == null || userId == Guid.Empty)
            return Result<CloseCashDrawerResultDto>.Failure("Auth.UserMissing", "No se encontró el usuario en sesión.");

        var session = await _dbContext.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.UserId == userId.Value && s.BranchId == request.BranchId && s.IsOpen, cancellationToken);

        if (session == null)
        {
            return Result<CloseCashDrawerResultDto>.Failure("Sales.NoActiveSession", "No existe una sesión activa para cerrar en esta sucursal.");
        }

        var cashSalesTotal = await _dbContext.Payments
            .Where(p => p.Sale != null && p.Sale.SessionId == session.Id && p.Method == PaymentMethod.Cash && p.Amount > 0m)
            .SumAsync(p => (decimal?)p.Amount, cancellationToken) ?? 0m;

        var cashRefundsTotal = await _dbContext.Payments
            .Where(p => p.Sale != null && p.Sale.SessionId == session.Id && p.Method == PaymentMethod.Cash && p.Amount < 0m)
            .SumAsync(p => (decimal?)-p.Amount, cancellationToken) ?? 0m;

        var manualCashInTotal = await _dbContext.CashMovements
            .Where(m => m.SessionId == session.Id && m.Type == CashMovementType.CashIn)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var manualCashOutTotal = await _dbContext.CashMovements
            .Where(m => m.SessionId == session.Id && m.Type == CashMovementType.CashOut)
            .SumAsync(m => (decimal?)m.Amount, cancellationToken) ?? 0m;

        var finalExpected = session.InitialBalance + cashSalesTotal - cashRefundsTotal + manualCashInTotal - manualCashOutTotal;
        session.Close(request.FinalBalanceEncounted, finalExpected);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new CloseCashDrawerResultDto(
            session.Id,
            session.InitialBalance,
            cashSalesTotal,
            cashRefundsTotal,
            manualCashInTotal,
            manualCashOutTotal,
            finalExpected,
            request.FinalBalanceEncounted,
            request.FinalBalanceEncounted - finalExpected
        );

        return Result<CloseCashDrawerResultDto>.Success(result);
    }
}
