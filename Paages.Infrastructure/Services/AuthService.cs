using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Paages.Domain.Entities;
using Paages.Domain.Exceptions;
using Paages.Infrastructure.Auth;
using Paages.Infrastructure.Data;

namespace Paages.Infrastructure.Services;

public class AuthService(PaagesDbContext db, IOptions<JwtOptions> jwtOptions)
{
    public async Task<AuthResult> RegisterAsync(string email, string password)
    {
        var normalizedEmail = Normalize(email);

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw new EmailAlreadyRegisteredException();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);

        var result = IssueTokenPair(user, Guid.NewGuid(), DateTime.UtcNow);
        await db.SaveChangesAsync();
        return result;
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var normalizedEmail = Normalize(email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new InvalidCredentialsException();

        var result = IssueTokenPair(user, Guid.NewGuid(), DateTime.UtcNow);
        await db.SaveChangesAsync();
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var found = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (found is null)
            throw new InvalidRefreshTokenException();

        var revoked = await db.RefreshTokens
            .Where(t => t.Id == found.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

        if (revoked == 0)
        {
            // reuse of an already-revoked token - burn the whole family
            await db.RefreshTokens
                .Where(t => t.FamilyId == found.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
            throw new InvalidRefreshTokenException();
        }

        if (DateTime.UtcNow - found.FamilyCreatedAt > TimeSpan.FromDays(jwtOptions.Value.AbsoluteRefreshDays))
            throw new InvalidRefreshTokenException();

        var user = await db.Users.FindAsync(found.UserId)
            ?? throw new InvalidRefreshTokenException();

        var result = IssueTokenPair(user, found.FamilyId, found.FamilyCreatedAt);
        await db.SaveChangesAsync();
        return result;
    }

    public async Task LogoutAsync(Guid userId, string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        var found = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (found is null || found.UserId != userId)
            throw new InvalidRefreshTokenException();

        found.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private AuthResult IssueTokenPair(User user, Guid familyId, DateTime familyCreatedAt)
    {
        var refreshTokenValue = GenerateRefreshTokenValue();

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshTokenValue),
            FamilyId = familyId,
            FamilyCreatedAt = familyCreatedAt,
            ExpiresAt = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        });

        return new AuthResult(GenerateAccessToken(user), refreshTokenValue);
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            Expires = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString()
            }
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static string GenerateRefreshTokenValue()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}