using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Domain.Enums;
using Paages.Domain.Exceptions;
using Paages.Domain.Interfaces;
using Paages.Infrastructure.Auth;
using Paages.Infrastructure.Data;

namespace Paages.Infrastructure.Services;

public class AccountTokenService(PaagesDbContext db, IEmailSender emailSender)
{
    private const int TokenTtlHours = 24;

    public async Task IssueEmailConfirmationAsync(User user, string baseUrl)
    {
        var link = await IssueAsync(user, AccountTokenPurpose.EmailConfirmation, baseUrl, "account/confirm-email");
        await emailSender.SendEmailConfirmationAsync(user.Email, link);
    }

    public async Task IssuePasswordResetAsync(User user, string baseUrl)
    {
        var link = await IssueAsync(user, AccountTokenPurpose.PasswordReset, baseUrl, "reset-password");
        await emailSender.SendPasswordResetAsync(user.Email, link);
    }

    public async Task IssueEmailChangeAsync(User user, string newEmail, string baseUrl)
    {
        var normalizedNewEmail = newEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == normalizedNewEmail))
            throw new EmailAlreadyRegisteredException();

        var link = await IssueAsync(user, AccountTokenPurpose.EmailChange, baseUrl, "account/confirm-email-change", normalizedNewEmail);
        await emailSender.SendEmailChangeAsync(normalizedNewEmail, link);
    }

    public async Task ConfirmEmailAsync(string rawToken)
    {
        var token = await FindValidAsync(rawToken, AccountTokenPurpose.EmailConfirmation);
        token.User.EmailConfirmedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ResetPasswordAsync(string rawToken, string newPassword)
    {
        var token = await FindValidAsync(rawToken, AccountTokenPurpose.PasswordReset);
        token.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        token.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await db.RefreshTokens.Where(t => t.UserId == token.UserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));
        await db.ApiTokens.Where(t => t.UserId == token.UserId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

        await emailSender.SendSecurityAlertAsync(token.User.Email, "Пароль изменён, все токены доступа отозваны.");
    }

    public async Task ConfirmEmailChangeAsync(string rawToken)
    {
        var token = await FindValidAsync(rawToken, AccountTokenPurpose.EmailChange);
        if (await db.Users.AnyAsync(u => u.Email == token.NewEmail && u.Id != token.UserId))
            throw new EmailAlreadyRegisteredException();

        token.User.Email = token.NewEmail!;
        token.User.EmailConfirmedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<string> IssueAsync(User user, AccountTokenPurpose purpose, string baseUrl, string path, string? newEmail = null)
    {
        var rawToken = SecureTokenGenerator.Generate();
        db.AccountTokens.Add(new AccountToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = purpose,
            TokenHash = SecureTokenGenerator.Hash(rawToken),
            NewEmail = newEmail,
            ExpiresAt = DateTime.UtcNow.AddHours(TokenTtlHours),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return $"{baseUrl.TrimEnd('/')}/{path}?token={rawToken}";
    }

    private async Task<AccountToken> FindValidAsync(string rawToken, AccountTokenPurpose purpose)
    {
        var tokenHash = SecureTokenGenerator.Hash(rawToken);
        var token = await db.AccountTokens.Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == tokenHash && t.Purpose == purpose);

        if (token is null || token.UsedAt is not null || token.ExpiresAt < DateTime.UtcNow)
            throw new InvalidAccountTokenException();

        return token;
    }
}
