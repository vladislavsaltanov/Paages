using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Paages.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Missing sub/NameIdentifier claim.");
        return Guid.Parse(raw);
    }
}