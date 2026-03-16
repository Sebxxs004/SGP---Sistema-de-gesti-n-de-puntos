using Gibag.Modules.Core.Domain;
using Gibag.Modules.Core.Infrastructure;
using Gibag.Shared.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gibag.Modules.Core.Presentation.Controllers;

[ApiController]
[Route("api/v1/core")]
[Authorize(Roles = "Admin")]
public class BranchesController : ControllerBase
{
    private readonly CoreDbContext _dbContext;
    private readonly ITenantService _tenantService;

    public BranchesController(CoreDbContext dbContext, ITenantService tenantService)
    {
        _dbContext = dbContext;
        _tenantService = tenantService;
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        var branches = await _dbContext.Branches
            .AsNoTracking()
            .OrderByDescending(b => b.IsActive)
            .ThenBy(b => b.Name)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.Address,
                b.Phone,
                b.Timezone,
                b.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = branches
        });
    }

    [HttpPost("branches")]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Tenant.Missing", message = "TenantId no disponible en el contexto." }
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "Nombre y direccion son obligatorios." }
            });
        }

        var branch = new Branch(
            tenantId.Value,
            request.Name.Trim(),
            request.Address.Trim(),
            string.IsNullOrWhiteSpace(request.Timezone) ? "America/Bogota" : request.Timezone.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim());

        await _dbContext.Branches.AddAsync(branch, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                branch.Id,
                branch.Name,
                branch.Address,
                branch.Phone,
                branch.Timezone,
                branch.IsActive
            }
        });
    }

    [HttpPut("branches/{id:guid}")]
    public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Branch.NotFound", message = "Sucursal no encontrada." }
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Validation.Invalid", message = "Nombre y direccion son obligatorios." }
            });
        }

        branch.Update(
            request.Name.Trim(),
            request.Address.Trim(),
            string.IsNullOrWhiteSpace(request.Timezone) ? "America/Bogota" : request.Timezone.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim());

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                branch.Id,
                branch.Name,
                branch.Address,
                branch.Phone,
                branch.Timezone,
                branch.IsActive
            }
        });
    }

    [HttpPatch("branches/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateBranch(Guid id, CancellationToken cancellationToken)
    {
        var branch = await _dbContext.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Branch.NotFound", message = "Sucursal no encontrada." }
            });
        }

        branch.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new { branch.Id, branch.IsActive }
        });
    }

    [HttpGet("company/settings")]
    public async Task<IActionResult> GetCompanySettings(CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Tenant.Missing", message = "TenantId no disponible en el contexto." }
            });
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);

        if (tenant == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Tenant.NotFound", message = "Empresa no encontrada." }
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                tenant.Id,
                tenant.Name,
                tenant.TaxId,
                thankYouMessage = tenant.ThankYouMessage
            }
        });
    }

    [HttpPut("company/settings")]
    public async Task<IActionResult> UpdateCompanySettings([FromBody] UpdateCompanySettingsRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.CurrentTenantId;
        if (tenantId == null || tenantId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Tenant.Missing", message = "TenantId no disponible en el contexto." }
            });
        }

        var tenant = await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
        if (tenant == null)
        {
            return NotFound(new
            {
                success = false,
                error = new { code = "Tenant.NotFound", message = "Empresa no encontrada." }
            });
        }

        tenant.UpdateThankYouMessage(request.ThankYouMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                tenant.Id,
                tenant.Name,
                tenant.TaxId,
                thankYouMessage = tenant.ThankYouMessage
            }
        });
    }
}

public sealed record CreateBranchRequest(string Name, string Address, string? Phone, string? Timezone);

public sealed record UpdateBranchRequest(string Name, string Address, string? Phone, string? Timezone);

public sealed record UpdateCompanySettingsRequest(string? ThankYouMessage);
