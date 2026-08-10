using Paages.Domain.Enums;

namespace Paages.Domain.Entities;

public class AccountToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public AccountTokenPurpose Purpose { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string? NewEmail { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
