using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Paages.Infrastructure.Auth;
using Paages.Infrastructure.Services;

namespace Paages.Api.Auth;

public class ApiTokenAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, ApiTokenService tokens)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var user = await tokens.ValidateAsync(header["Bearer ".Length..].Trim());
        if (user is null) return AuthenticateResult.Fail("Invalid or revoked token");

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(AuthClaimsFactory.Build(user, Scheme.Name)), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
