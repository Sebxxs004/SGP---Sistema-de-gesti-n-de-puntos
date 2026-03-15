using Gibag.Modules.Sales.Domain;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;

public class OpenCashDrawerCommandHandler : IRequestHandler<OpenCashDrawerCommand, Result<Guid>>
{
    private readonly SalesDbContext _dbContext;
    private readonly ITenantService _tenantService;
    private readonly ICurrentUser _currentUser;

    public OpenCashDrawerCommandHandler(SalesDbContext dbContext, ITenantService tenantService, ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(OpenCashDrawerCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
            return Result<Guid>.Failure("Auth.TenantMissing", "No se encontró el inquilino.");

        var userId = _currentUser.Id;
        if (userId == null || userId == Guid.Empty)
            return Result<Guid>.Failure("Auth.UserMissing", "No se encontró el usuario en sesión.");

        // Check if there is already an open session for this branch (and optionally user)
        bool openSessionExists = await _dbContext.CashRegisterSessions
            .AnyAsync(s => s.BranchId == request.BranchId && s.IsOpen, cancellationToken);

        if (openSessionExists)
        {
            return Result<Guid>.Failure("Sales.SessionActive", "Ya existe una caja abierta en esta sucursal.");
        }

        var session = new CashRegisterSession(
            tenantId.Value,
            request.BranchId,
            userId.Value,
            request.InitialAmount
        );

        await _dbContext.CashRegisterSessions.AddAsync(session, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(session.Id);
    }
}
