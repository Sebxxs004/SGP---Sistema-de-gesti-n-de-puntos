using Gibag.Modules.Core.Domain;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Core.Presentation.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Cajero"
    };

    private readonly CoreDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public UsersController(CoreDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .Include(u => u.UserBranches)
                .ThenInclude(ub => ub.Branch)
            .Select(u => new UserListItemDto(
                u.Id,
                $"{u.FirstName} {u.LastName}".Trim(),
                u.Email,
                u.Role != null ? u.Role.Name : "Sin rol",
                u.UserBranches
                    .Where(ub => ub.Branch != null)
                    .Select(ub => new UserBranchDto(
                        ub.BranchId,
                        ub.Branch!.Name,
                        ub.IsPrimary
                    ))
                    .ToList()
            ))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = users
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Tenant.Missing", message = "TenantId no disponible en el contexto." }
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Role) ||
            request.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "Todos los campos son obligatorios." }
            });
        }

        if (!AllowedRoles.Contains(request.Role))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.InvalidRole", message = "El rol debe ser Admin o Cajero." }
            });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "User.EmailExists", message = "Ya existe un usuario con ese correo en este tenant." }
            });
        }

        var branch = await _dbContext.Branches
            .FirstOrDefaultAsync(b => b.Id == request.BranchId && b.IsActive, cancellationToken);

        if (branch is null)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.NotFound", message = "La sucursal asignada no existe o está inactiva." }
            });
        }

        var roleName = request.Role.Trim();
        var role = await _dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name.ToLower() == roleName.ToLower(), cancellationToken);

        if (role is null)
        {
            var permissions = string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) ? "[\"all\"]" : "[]";
            role = new Role(tenantId.Value, roleName, permissions);
            await _dbContext.Roles.AddAsync(role, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var displayName = request.Name.Trim();
        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var firstName = parts.FirstOrDefault() ?? displayName;
        var lastName = parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty;

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new User(tenantId.Value, role.Id, normalizedEmail, passwordHash, firstName, lastName);
        await _dbContext.Users.AddAsync(user, cancellationToken);

        var userBranch = new UserBranch(tenantId.Value, user.Id, request.BranchId, isPrimary: true);
        await _dbContext.UserBranches.AddAsync(userBranch, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                id = user.Id,
                name = displayName,
                email = user.Email,
                role = role.Name,
                branchId = request.BranchId
            }
        });
    }
}

public sealed record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    Guid BranchId,
    string Role
);

public sealed record UserBranchDto(Guid Id, string Name, bool IsPrimary);

public sealed record UserListItemDto(Guid Id, string Name, string Email, string Role, IReadOnlyList<UserBranchDto> Branches);
