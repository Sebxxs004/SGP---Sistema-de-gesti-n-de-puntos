using Gibag.Modules.Sales.Application.Sales.CreateSale;
using Gibag.Modules.Sales.Application.Sessions.CloseCashDrawer;
using Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;
using Gibag.Modules.Sales.Infrastructure;
using Gibag.Shared.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Gibag.Modules.Sales.Presentation.Controllers;

[ApiController]
[Route("api/v1/sales")]
public class SalesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;
    private readonly SalesDbContext _dbContext;

    public SalesController(ISender sender, ICurrentUser currentUser, SalesDbContext dbContext)
    {
        _sender = sender;
        _currentUser = currentUser;
        _dbContext = dbContext;
    }

    [HttpGet("sessions/active")]
    public async Task<IActionResult> GetActiveSession(CancellationToken cancellationToken)
    {
        var currentBranchId = _currentUser.BranchId;
        var currentUserId = _currentUser.Id;

        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para consultar la sesión activa." }
            });
        }

        if (currentUserId == null || currentUserId == Guid.Empty)
        {
            return Unauthorized(new
            {
                success = false,
                error = new { code = "Auth.UserMissing", message = "No se encontró usuario autenticado." }
            });
        }

        var activeSession = await _dbContext.CashRegisterSessions
            .AsNoTracking()
            .Where(s => s.UserId == currentUserId.Value && s.BranchId == currentBranchId.Value && s.IsOpen)
            .Select(s => new
            {
                s.Id,
                s.BranchId,
                s.UserId,
                s.OpenedAt,
                s.InitialBalance,
                s.IsOpen
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            data = activeSession
        });
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> OpenCashDrawer([FromBody] OpenCashDrawerCommand command)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para abrir caja." }
            });
        }

        var result = await _sender.Send(command with { BranchId = currentBranchId.Value });

        if (result.IsFailure)
        {
            return BadRequest(new { 
                success = false, 
                error = new { code = result.ErrorCode, message = result.ErrorMessage } 
            });
        }

        return Created($"/api/v1/sales/sessions/{result.Value}", new { 
            success = true, 
            data = new { id = result.Value } 
        });
    }

    [HttpPost("sessions/close")]
    public async Task<IActionResult> CloseCashDrawer([FromBody] CloseCashDrawerCommand command)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para cerrar caja." }
            });
        }

        var result = await _sender.Send(command with { BranchId = currentBranchId.Value });

        if (result.IsFailure)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = result.ErrorCode, message = result.ErrorMessage }
            });
        }

        return Ok(new
        {
            success = true,
            data = result.Value
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleCommand command)
    {
        var currentBranchId = _currentUser.BranchId;
        if (currentBranchId == null || currentBranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para registrar ventas." }
            });
        }

        var result = await _sender.Send(command with { BranchId = currentBranchId.Value });

        if (result.IsFailure)
        {
            return BadRequest(new { 
                success = false, 
                error = new { code = result.ErrorCode, message = result.ErrorMessage } 
            });
        }

        // 201 Created or 200 OK since client might provide ID
        return Ok(new { 
            success = true, 
            data = new { id = result.Value } 
        });
    }
}
