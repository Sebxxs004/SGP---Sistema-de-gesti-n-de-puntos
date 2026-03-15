using Gibag.Modules.Core.Domain;

namespace Gibag.Modules.Core.Application.Interfaces;

public interface IJwtProvider
{
    string GenerateToken(User user);
}
