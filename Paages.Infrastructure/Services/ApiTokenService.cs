using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Infrastructure.Auth;
using Paages.Infrastructure.Data;

namespace Paages.Infrastructure.Services;

public class ApiTokenService(PaagesDbContext db)
{
    public async Task<(ApiToken Token, string RawToken)> IssueAsync(Guid userId, string name)
    {
        var rawToken = SecureTokenGenerator.Generate();
        var token = new ApiToken
        {
            Id = Guid.NewGuid(), UserId = userId, Name = name,
            TokenHash = SecureTokenGenerator.Hash(rawToken), CreatedAt = DateTime.UtcNow
        };
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync();
        return (token, rawToken);
    }

    public async Task<User?> ValidateAsync(string rawToken)
    {
        var tokenHash = SecureTokenGenerator.Hash(rawToken);
        var token = await db.ApiTokens.Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token is null || token.RevokedAt is not null || (token.ExpiresAt is not null && token.ExpiresAt < DateTime.UtcNow))
            return null;

        token.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return token.User;
    }

    public Task RevokeAsync(Guid userId, Guid tokenId) =>
        db.ApiTokens.Where(t => t.Id == tokenId && t.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

    public Task RevokeAllAsync(Guid userId) =>
        db.ApiTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

    public Task<List<ApiToken>> ListAsync(Guid userId) =>
        db.ApiTokens.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).ToListAsync();
}
