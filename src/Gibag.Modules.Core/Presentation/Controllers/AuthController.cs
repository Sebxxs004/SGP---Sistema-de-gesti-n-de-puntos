using Gibag.Modules.Core.Application.Auth.Login;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Gibag.Modules.Core.Presentation.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _sender.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { 
                success = false, 
                error = new { code = result.ErrorCode, message = result.ErrorMessage } 
            });
        }

        return Ok(new { 
            success = true, 
            data = new { token = result.Value } 
        });
    }
}
