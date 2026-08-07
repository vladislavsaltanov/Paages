using System.Security.Claims;
using Paages.Api.Contracts;
using Paages.Infrastructure.Auth;
using Paages.Infrastructure.Services;

namespace Paages.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/auth");

        group.MapPost("/register", async (RegisterRequest request, AuthService authService) =>
            Results.Ok(ToResponse(await authService.RegisterAsync(request.Email, request.Password))))
            .RequireRateLimiting("auth");

        group.MapPost("/login", async (LoginRequest request, AuthService authService) =>
            Results.Ok(ToResponse(await authService.LoginAsync(request.Email, request.Password))))
            .RequireRateLimiting("auth");

        group.MapPost("/refresh", async (RefreshRequest request, AuthService authService) =>
            Results.Ok(ToResponse(await authService.RefreshAsync(request.RefreshToken))))
            .RequireRateLimiting("auth");

        group.MapPost("/logout", async (LogoutRequest request, ClaimsPrincipal user, AuthService authService) =>
        {
            await authService.LogoutAsync(user.GetUserId(), request.RefreshToken);
            return Results.NoContent();
        }).RequireRateLimiting("auth").RequireAuthorization();
    }

    private static AuthResponse ToResponse(AuthResult result) => new(result.AccessToken, result.RefreshToken);
}