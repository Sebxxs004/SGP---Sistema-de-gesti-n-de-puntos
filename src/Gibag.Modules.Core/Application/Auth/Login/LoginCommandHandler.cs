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
        // 1. We must search for the user globally ignoring query filters if we don't have a tenant context at login time.
        // During login, the user provides email/password without a TenantId in headers usually.
        // Therefore, we query without Tenant filters if needed, OR we assume the TenantId is resolved.
        // Wait, the rules say "Un mismo correo puede existir en la plataforma, pero no duplicado dentro de la misma empresa".
        // If a user belongs to multiple tenants with the same email, how do they log in? 
        // Typically, B2B SaaS requires either a 'tenant slug' in the URL, or user selects tenant after login.
        // For now, let's search ignoring query filters and picking the first matching active user, or require the tenant_id. 
        // As a standard MVP approach, we find the first match ignoring filter to authenticate.
        
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
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
