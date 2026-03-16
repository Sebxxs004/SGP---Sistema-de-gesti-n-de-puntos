using Gibag.Modules.Sales.Infrastructure;
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

        var salesTotal = await _dbContext.Sales
            .Where(s => s.SessionId == session.Id)
            .SumAsync(s => (decimal?)s.Total, cancellationToken) ?? 0m;

        var finalExpected = session.InitialBalance + salesTotal;
        session.Close(request.FinalBalanceEncounted, finalExpected);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = new CloseCashDrawerResultDto(
            session.Id,
            session.InitialBalance,
            salesTotal,
            finalExpected,
            request.FinalBalanceEncounted,
            request.FinalBalanceEncounted - finalExpected
        );

        return Result<CloseCashDrawerResultDto>.Success(result);
    }
}
