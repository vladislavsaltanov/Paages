using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Paages.Domain.Interfaces;

namespace Paages.Web.Services;

public class CurrentUser(AuthenticationStateProvider authProvider) : ICurrentUser
{
    public async Task<Guid> GetIdAsync()
    {
        var state = await authProvider.GetAuthenticationStateAsync();
        var raw = state.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(raw, out var id) || id == Guid.Empty)
            throw new InvalidOperationException(
                "Cannot determine the current user. Claim NameIdentifier is missing.");

        return id;
    }
}