using Gibag.Modules.Sales.Application.Sales.CreateSale;
using Gibag.Modules.Sales.Application.Sessions.OpenCashDrawer;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gibag.Modules.Sales.Presentation.Controllers;

[ApiController]
[Route("api/v1/sales")]
public class SalesController : ControllerBase
{
    private readonly ISender _sender;

    public SalesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> OpenCashDrawer([FromBody] OpenCashDrawerCommand command)
    {
        var result = await _sender.Send(command);

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
        var result = await _sender.Send(command);

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
