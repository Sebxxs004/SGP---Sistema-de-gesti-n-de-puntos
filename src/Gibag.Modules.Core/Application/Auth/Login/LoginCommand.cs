using Gibag.Shared.Models;
using MediatR;

namespace Gibag.Modules.Core.Application.Auth.Login;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<Result<string>>;
