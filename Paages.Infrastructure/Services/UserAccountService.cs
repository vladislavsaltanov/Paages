using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Domain.Exceptions;
using Paages.Infrastructure.Data;

namespace Paages.Infrastructure.Services;

public class UserAccountService(PaagesDbContext db)
{
    public async Task<User> RegisterUserAsync(string email, string password)
    {
        var normalizedEmail = Normalize(email);

        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw new EmailAlreadyRegisteredException();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            Nickname = DefaultNickname(normalizedEmail),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public async Task<User> ValidateCredentialsAsync(string email, string password)
    {
        var normalizedEmail = Normalize(email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail);

        var valid = user is not null && user.PasswordHash is not null
            && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

        // artificial wait to protect from password guessing
        await Task.Delay(Random.Shared.Next(2000, 3000));

        if (!valid) throw new InvalidCredentialsException();
        return user!;
    }

    public async Task<User> FindOrCreateGoogleUserAsync(string googleId, string email, bool emailVerified)
    {
        var normalizedEmail = Normalize(email);

        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleId == googleId)
            ?? await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                Nickname = DefaultNickname(normalizedEmail),
                GoogleId = googleId,
                EmailConfirmedAt = emailVerified ? DateTime.UtcNow : null,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }
        else
        {
            if (user.GoogleId is null) user.GoogleId = googleId;
            if (user.EmailConfirmedAt is null && emailVerified) user.EmailConfirmedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return user;
    }

    public Task<User?> FindByEmailAsync(string email) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == Normalize(email));

    public async Task<User> UpdateNicknameAsync(Guid userId, string nickname)
    {
        var user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException("User not found");
        if (!string.IsNullOrWhiteSpace(nickname)) user.Nickname = nickname.Trim();
        await db.SaveChangesAsync();
        return user;
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string DefaultNickname(string normalizedEmail) => normalizedEmail.Split('@')[0];
}
