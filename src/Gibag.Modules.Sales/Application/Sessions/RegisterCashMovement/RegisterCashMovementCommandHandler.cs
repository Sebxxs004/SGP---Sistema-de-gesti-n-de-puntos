using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Sales.Application.Sessions.RegisterCashMovement;

public class RegisterCashMovementCommandHandler : IRequestHandler<RegisterCashMovementCommand, Result<Guid>>
{
    private readonly SalesDbContext _dbContext;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUser _currentUser;

    public RegisterCashMovementCommandHandler(SalesDbContext dbContext, ITenantService tenantService, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(RegisterCashMovementCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino.");

        var userId = _currentUser.Id;
        if (userId == null || userId == Guid.Empty)
            return Result<Guid>.Failure("Auth.UserMissing", "No se encontró el usuario en sesión.");

        var session = await _dbContext.CashRegisterSessions
            .FirstOrDefaultAsync(s => s.UserId == userId.Value && s.BranchId == request.BranchId && s.IsOpen, cancellationToken);

        if (session == null)
            return Result<Guid>.Failure("Sales.NoActiveSession", "No existe una sesión activa para registrar movimientos.");

        var movement = new CashMovement(
            tenantId.Value,
            session.Id,
            request.Type,
            request.Amount,
            request.Reason
        );

        await _dbContext.CashMovements.AddAsync(movement, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(movement.Id);
    }
}
