using Gibag.Modules.Inventory.Application.Products.CreateProduct;
using Gibag.Shared.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gibag.Modules.Inventory.Presentation.Controllers;

[ApiController]
[Route("api/v1/inventory")]
// Uncomment this when Authorize middleware is fully working
// [Authorize] 
public class InventoryController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public InventoryController(ISender sender, ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductCommand command)
    {
        if (_currentUser.BranchId == null || _currentUser.BranchId == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = "Branch.Required", message = "Debes enviar X-Branch-Id para operar inventario." }
            });
        }

        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { 
                success = false, 
                error = new { code = result.ErrorCode, message = result.ErrorMessage } 
            });
        }

        return Created($"/api/v1/inventory/products/{result.Value}", new { 
            success = true, 
            data = new { id = result.Value } 
        });
    }
}
