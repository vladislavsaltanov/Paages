namespace Paages.Domain.Interfaces;

public interface ICurrentUser
{
    Task<Guid> GetIdAsync();
}