using Gibag.Modules.Core.Application.Interfaces;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Core.Application.Auth.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<string>>
{
    private readonly CoreDbContext _dbContext;
    private readonly IJwtProvider _jwtProvider;

    public LoginCommandHandler(CoreDbContext dbContext, IJwtProvider jwtProvider)
    {
        _dbContext = dbContext;
        _jwtProvider = jwtProvider;
    }

    public async Task<Result<string>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // El cliente debe enviar el header X-Tenant-Id al hacer login.
        // El TenantResolutionMiddleware inyecta el TenantId en el contexto,
        // por lo que el CoreDbContext filtrará automáticamente la búsqueda al Tenant correcto.
        
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null)
        {
            return Result<string>.Failure("Auth.InvalidCredentials", "Credenciales incorrectas.");
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

        if (!isPasswordValid)
        {
            return Result<string>.Failure("Auth.InvalidCredentials", "Credenciales incorrectas.");
        }

        // Generate JWT
        string token = _jwtProvider.GenerateToken(user);

        return Result<string>.Success(token);
    }
}
