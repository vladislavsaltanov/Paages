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

        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new InvalidCredentialsException();

        return user;
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}