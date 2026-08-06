using System.ComponentModel.DataAnnotations;

namespace Paages.Infrastructure.Auth;

public class JwtOptions
{
    [Required] public string Issuer { get; set; } = string.Empty;
    [Required] public string Audience { get; set; } = string.Empty;
    [Required] public string SigningKey { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int AccessTokenMinutes { get; set; } = 15;
    [Range(1, int.MaxValue)] public int RefreshTokenDays { get; set; } = 14;
    [Range(1, int.MaxValue)] public int AbsoluteRefreshDays { get; set; } = 60;
}