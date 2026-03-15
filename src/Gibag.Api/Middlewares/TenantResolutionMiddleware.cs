using Gibag.Shared.Interfaces;

namespace Gibag.Api.Middlewares;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ITenantService tenantService)
    {
        // En una implementación real con JWT configurado, se extraería así:
        // var tenantIdClaim = context.User.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        
        // Simulando que extraemos el tenant de un Header o Claim para avanzar
        var tenantIdString = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        
        // Si no está en el header, podemos revisar si hay un usuario logueado (simulación claims)
        if (string.IsNullOrEmpty(tenantIdString))
        {
            tenantIdString = context.User.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value;
        }

        if (Guid.TryParse(tenantIdString, out var tenantId))
        {
            tenantService.SetCurrentTenantId(tenantId);
        }

        await _next(context);
    }
}
