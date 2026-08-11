using Paages.Domain.Interfaces;
using Paages.Infrastructure.Auth;

namespace Paages.Api.Auth;

public class ApiCurrentUser(IHttpContextAccessor httpContextAccessor): ICurrentUser
{
    public Task<Guid> GetIdAsync()
    {
        var user = httpContextAccessor.HttpContext?.User ?? 
            throw new InvalidOperationException("No HttpContext available.");

        return Task.FromResult(user.GetUserId());
    }
}