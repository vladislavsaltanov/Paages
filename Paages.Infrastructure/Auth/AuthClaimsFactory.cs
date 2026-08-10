using System.Security.Claims;
using Paages.Domain.Entities;

namespace Paages.Infrastructure.Auth;

public static class AuthClaimsFactory
{
    public static ClaimsIdentity Build(User user, string authenticationType) =>
        new(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Email)
            ],
            authenticationType);
}
