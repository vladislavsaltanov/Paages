namespace Paages.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string? GoogleId { get; set; }
    public DateTime? EmailConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}