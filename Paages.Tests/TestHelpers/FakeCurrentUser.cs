using Paages.Domain.Interfaces;

namespace Paages.Tests.TestHelpers;

public class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Task<Guid> GetIdAsync() => Task.FromResult(userId);
}