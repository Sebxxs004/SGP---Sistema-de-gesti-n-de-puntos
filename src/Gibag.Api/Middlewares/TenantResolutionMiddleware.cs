using Gibag.Api.Services;
using Gibag.Shared.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Gibag.Api.Middlewares;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, CurrentTenantService currentContext)
    {
        var tenantIdString = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(tenantIdString))
        {
            tenantIdString = context.User.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        }

        if (Guid.TryParse(tenantIdString, out var tenantId))
        {
            currentContext.SetCurrentTenantId(tenantId);
        }

        var userIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            currentContext.Id = userId;
        }

        currentContext.Email = context.User.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")?.Value;
        currentContext.Role = context.User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")?.Value;

        var branchIdString = context.Request.Headers["X-Branch-Id"].FirstOrDefault();
        var requiresBranchHeader = context.Request.Path.StartsWithSegments("/api/v1/inventory", StringComparison.OrdinalIgnoreCase)
                                 || context.Request.Path.StartsWithSegments("/api/v1/sales", StringComparison.OrdinalIgnoreCase);

        if (requiresBranchHeader)
        {
            if (string.IsNullOrWhiteSpace(branchIdString))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    error = new { code = "Branch.Required", message = "El header X-Branch-Id es obligatorio para inventario y ventas." }
                });
                return;
            }

            if (!Guid.TryParse(branchIdString, out var branchId))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    error = new { code = "Branch.Invalid", message = "El header X-Branch-Id no tiene un formato GUID válido." }
                });
                return;
            }

            currentContext.SetCurrentBranchId(branchId);
        }
        else if (Guid.TryParse(branchIdString, out var optionalBranchId))
        {
            currentContext.SetCurrentBranchId(optionalBranchId);
        }

        await _next(context);
    }
}
