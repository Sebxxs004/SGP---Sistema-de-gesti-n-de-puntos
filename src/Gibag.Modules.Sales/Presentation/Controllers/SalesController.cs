using Gibag.Modules.Sales.Application.Sales.CreateSale;
using Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;
using Gibag.Shared.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gibag.Modules.Sales.Presentation.Controllers;

[ApiController]
[Route("api/v1/sales")]
public class SalesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public SalesController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
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
