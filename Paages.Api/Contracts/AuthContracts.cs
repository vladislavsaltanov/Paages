using System.ComponentModel.DataAnnotations;

namespace Paages.Api.Contracts;

public record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record RefreshRequest([property: Required] string RefreshToken);

public record LogoutRequest([property: Required] string RefreshToken);

public record AuthResponse(string AccessToken, string RefreshToken);