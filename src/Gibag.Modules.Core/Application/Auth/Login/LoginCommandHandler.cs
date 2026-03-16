using Gibag.Modules.Core.Application.Interfaces;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Core.Application.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly CoreDbContext _dbContext;
    private readonly IJwtProvider _jwtProvider;

    public LoginCommandHandler(CoreDbContext dbContext, IJwtProvider jwtProvider)
    {
        _dbContext = dbContext;
        _jwtProvider = jwtProvider;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // El cliente debe enviar el header X-Tenant-Id al hacer login.
        // El TenantResolutionMiddleware inyecta el TenantId en el contexto,
        // por lo que el CoreDbContext filtrará automáticamente la búsqueda al Tenant correcto.
        
        var user = await _dbContext.Users
            .Include(u => u.Role)
            .Include(u => u.UserBranches)
                .ThenInclude(ub => ub.Branch)
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null)
        {
            return Result<LoginResponse>.Failure("Auth.InvalidCredentials", "Credenciales incorrectas.");
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            return Result<LoginResponse>.Failure("Auth.InvalidCredentials", "Credenciales incorrectas.");
        }

        var branches = user.UserBranches
            .Where(ub => ub.Branch is not null && ub.Branch.IsActive)
            .Select(ub => new LoginBranchDto(ub.BranchId, ub.Branch!.Name, ub.IsPrimary))
            .ToList();

        if (branches.Count == 0)
        {
            return Result<LoginResponse>.Failure("Auth.NoBranches", "El usuario no tiene sucursales asignadas.");
        }

        var defaultBranchId = branches
            .Where(b => b.IsPrimary)
            .Select(b => (Guid?)b.Id)
            .FirstOrDefault();

        // Generate JWT
        string token = _jwtProvider.GenerateToken(user);

        var response = new LoginResponse(
            token,
            user.Id,
            user.Email,
            user.Role?.Name ?? string.Empty,
            defaultBranchId,
            branches
        );

        return Result<LoginResponse>.Success(response);
    }
}
