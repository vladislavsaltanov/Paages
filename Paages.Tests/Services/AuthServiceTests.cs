using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Paages.Domain.Entities;
using Paages.Domain.Exceptions;
using Paages.Infrastructure.Auth;
using Paages.Infrastructure.Data;
using Paages.Infrastructure.Services;

namespace Paages.Tests.Services;

public class AuthServiceTests : IAsyncLifetime
{
    private const string Issuer = "paages-tests";
    private const string Audience = "paages-tests-aud";
    private const string SigningKey = "test-signing-key-never-use-this-in-production-1234567890ab";

    private SqliteConnection _connection = null!;
    private PaagesDbContext _db = null!;
    private UserAccountService _accounts = null!;
    private AuthService _sut = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PaagesDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new PaagesDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = SigningKey,
            AccessTokenMinutes = 15,
            RefreshTokenDays = 14,
            AbsoluteRefreshDays = 60
        });

        _accounts = new UserAccountService(_db);
        _sut = new AuthService(_db, jwtOptions, _accounts);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private async Task<User> SeedUserAsync(string email = "seed@paages.dev")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("irrelevant"),
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<(RefreshToken Token, string RawValue)> SeedRefreshTokenAsync(
        Guid userId, Guid? familyId = null, DateTime? familyCreatedAt = null,
        DateTime? expiresAt = null, DateTime? revokedAt = null)
    {
        var raw = Guid.NewGuid().ToString("N");
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant(),
            FamilyId = familyId ?? Guid.NewGuid(),
            FamilyCreatedAt = familyCreatedAt ?? DateTime.UtcNow,
            ExpiresAt = expiresAt ?? DateTime.UtcNow.AddDays(14),
            RevokedAt = revokedAt,
            CreatedAt = DateTime.UtcNow
        };
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync();
        return (token, raw);
    }

    [Fact]
    public async Task RegisterUserAsync_NewEmail_CreatesUserWithNormalizedEmail()
    {
        var user = await _accounts.RegisterUserAsync("  User@Example.com  ", "password123");

        Assert.Equal("user@example.com", user.Email);
    }

    [Fact]
    public async Task RegisterUserAsync_HashesPassword_NotStoredInPlainText()
    {
        var user = await _accounts.RegisterUserAsync("plain@test.dev", "supersecret");

        Assert.NotEqual("supersecret", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("supersecret", user.PasswordHash));
    }

    [Fact]
    public async Task RegisterUserAsync_DuplicateEmailExactCase_Throws()
    {
        await _accounts.RegisterUserAsync("dup@test.dev", "password123");

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => _accounts.RegisterUserAsync("dup@test.dev", "otherpassword"));
    }

    [Fact]
    public async Task RegisterUserAsync_DuplicateEmailDifferentCase_Throws()
    {
        await _accounts.RegisterUserAsync("Dup@Test.dev", "password123");

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => _accounts.RegisterUserAsync("dup@test.DEV", "otherpassword"));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_CorrectPassword_ReturnsUser()
    {
        await _accounts.RegisterUserAsync("valid@test.dev", "correctpassword");

        var user = await _accounts.ValidateCredentialsAsync("valid@test.dev", "correctpassword");

        Assert.Equal("valid@test.dev", user.Email);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WrongPassword_Throws()
    {
        await _accounts.RegisterUserAsync("wrongpw@test.dev", "correctpassword");

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _accounts.ValidateCredentialsAsync("wrongpw@test.dev", "wrongpassword"));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_UnknownEmail_ThrowsSameExceptionAsWrongPassword()
    {
        // same exception on purpose - don't leak which part was wrong
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _accounts.ValidateCredentialsAsync("ghost@test.dev", "whatever"));
    }

    [Fact]
    public async Task ValidateCredentialsAsync_EmailDifferentCaseAndWhitespace_StillMatches()
    {
        await _accounts.RegisterUserAsync("case@test.dev", "password123");

        var user = await _accounts.ValidateCredentialsAsync("  CASE@Test.DEV  ", "password123");

        Assert.Equal("case@test.dev", user.Email);
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsNonEmptyTokenPair()
    {
        var result = await _sut.RegisterAsync("register@test.dev", "password123");

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
    }

    [Fact]
    public async Task RegisterAsync_PersistsRefreshTokenHashNotRawValue()
    {
        var result = await _sut.RegisterAsync("hashcheck@test.dev", "password123");

        var stored = await _db.RefreshTokens.SingleAsync();
        Assert.NotEqual(result.RefreshToken, stored.TokenHash);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_PropagatesEmailAlreadyRegistered()
    {
        await _sut.RegisterAsync("propagate@test.dev", "password123");

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => _sut.RegisterAsync("propagate@test.dev", "password456"));
    }

    [Fact]
    public async Task RegisterAsync_TwoDifferentUsers_GetDifferentFamilyIds()
    {
        await _sut.RegisterAsync("user1@test.dev", "password123");
        await _sut.RegisterAsync("user2@test.dev", "password123");

        var families = await _db.RefreshTokens.Select(t => t.FamilyId).ToListAsync();
        Assert.Equal(2, families.Distinct().Count());
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsTokenPair()
    {
        await _accounts.RegisterUserAsync("login@test.dev", "password123");

        var result = await _sut.LoginAsync("login@test.dev", "password123");

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Throws()
    {
        await _accounts.RegisterUserAsync("loginwrong@test.dev", "password123");

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => _sut.LoginAsync("loginwrong@test.dev", "wrongpassword"));
    }

    [Fact]
    public async Task LoginAsync_EachCall_MintsNewFamilyId()
    {
        var user = await _accounts.RegisterUserAsync("multilogin@test.dev", "password123");

        await _sut.LoginAsync("multilogin@test.dev", "password123");
        await _sut.LoginAsync("multilogin@test.dev", "password123");

        var families = await _db.RefreshTokens.Where(t => t.UserId == user.Id)
            .Select(t => t.FamilyId).ToListAsync();
        Assert.Equal(2, families.Distinct().Count());
    }

    [Fact]
    public async Task LoginAsync_AccessToken_HasCorrectSubjectIssuerAudience()
    {
        var user = await _accounts.RegisterUserAsync("claims@test.dev", "password123");

        var result = await _sut.LoginAsync("claims@test.dev", "password123");

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(result.AccessToken,
            new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                ValidateLifetime = true
            });

        Assert.True(validation.IsValid);
        var sub = validation.ClaimsIdentity!.FindFirst(JwtRegisteredClaimNames.Sub)!.Value;
        Assert.Equal(user.Id.ToString(), sub);
    }

    [Fact]
    public async Task RefreshAsync_ValidToken_RevokesOldAndIssuesNewInSameFamily()
    {
        var user = await SeedUserAsync();
        var familyId = Guid.NewGuid();
        var familyCreatedAt = DateTime.UtcNow.AddDays(-1);
        var (old, raw) = await SeedRefreshTokenAsync(user.Id, familyId, familyCreatedAt);

        var result = await _sut.RefreshAsync(raw);
        _db.ChangeTracker.Clear();

        var reloadedOld = await _db.RefreshTokens.FindAsync(old.Id);
        Assert.NotNull(reloadedOld!.RevokedAt);
        var newToken = await _db.RefreshTokens.SingleAsync(t => t.Id != old.Id);
        Assert.Equal(familyId, newToken.FamilyId);
        Assert.Equal(familyCreatedAt, newToken.FamilyCreatedAt);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
    }

    [Fact]
    public async Task RefreshAsync_UnknownToken_Throws()
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _sut.RefreshAsync("this-token-was-never-issued"));
    }

    [Fact]
    public async Task RefreshAsync_ExpiredButNotRevoked_ThrowsAndLeavesTokenUnrevoked()
    {
        var user = await SeedUserAsync();
        var (expired, raw) = await SeedRefreshTokenAsync(user.Id, expiresAt: DateTime.UtcNow.AddDays(-1));

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.RefreshAsync(raw));

        var reloaded = await _db.RefreshTokens.FindAsync(expired.Id);
        Assert.Null(reloaded!.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_AlreadyRevokedToken_ThrowsReuseDetected()
    {
        var user = await SeedUserAsync();
        var (_, raw) = await SeedRefreshTokenAsync(user.Id, revokedAt: DateTime.UtcNow.AddMinutes(-5));

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.RefreshAsync(raw));
    }

    [Fact]
    public async Task RefreshAsync_ReuseInFamilyWithOtherValidTokens_RevokesAllOfThem()
    {
        var user = await SeedUserAsync();
        var familyId = Guid.NewGuid();
        var (_, usedUpRaw) = await SeedRefreshTokenAsync(user.Id, familyId, revokedAt: DateTime.UtcNow.AddMinutes(-5));
        var (stillValid, _) = await SeedRefreshTokenAsync(user.Id, familyId);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.RefreshAsync(usedUpRaw));

        _db.ChangeTracker.Clear();

        var reloadedValid = await _db.RefreshTokens.FindAsync(stillValid.Id);
        Assert.NotNull(reloadedValid!.RevokedAt);
    }

    [Fact]
    public async Task RefreshAsync_PastAbsoluteCap_Throws()
    {
        var user = await SeedUserAsync();
        var (_, raw) = await SeedRefreshTokenAsync(user.Id, familyCreatedAt: DateTime.UtcNow.AddDays(-61));

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.RefreshAsync(raw));
    }

    [Fact]
    public async Task RefreshAsync_JustUnderAbsoluteCap_Succeeds()
    {
        var user = await SeedUserAsync();
        var (_, raw) = await SeedRefreshTokenAsync(user.Id, familyCreatedAt: DateTime.UtcNow.AddDays(-59));

        var result = await _sut.RefreshAsync(raw);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public async Task RefreshAsync_ChainedRefreshes_PreserveFamilyIdAndFamilyCreatedAt()
    {
        var user = await SeedUserAsync();
        var familyId = Guid.NewGuid();
        var familyCreatedAt = DateTime.UtcNow.AddDays(-10);
        var (_, firstRaw) = await SeedRefreshTokenAsync(user.Id, familyId, familyCreatedAt);

        var first = await _sut.RefreshAsync(firstRaw);
        await _sut.RefreshAsync(first.RefreshToken);

        var allInFamily = await _db.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync();
        Assert.Equal(3, allInFamily.Count);
        Assert.All(allInFamily, t => Assert.Equal(familyCreatedAt, t.FamilyCreatedAt));
    }

    [Fact]
    public async Task LogoutAsync_OwnToken_RevokesIt()
    {
        var user = await SeedUserAsync();
        var (token, raw) = await SeedRefreshTokenAsync(user.Id);

        await _sut.LogoutAsync(user.Id, raw);

        var reloaded = await _db.RefreshTokens.FindAsync(token.Id);
        Assert.NotNull(reloaded!.RevokedAt);
    }

    [Fact]
    public async Task LogoutAsync_TokenBelongsToDifferentUser_ThrowsAndDoesNotRevoke()
    {
        var owner = await SeedUserAsync("owner@test.dev");
        var stranger = await SeedUserAsync("stranger@test.dev");
        var (token, raw) = await SeedRefreshTokenAsync(owner.Id);

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _sut.LogoutAsync(stranger.Id, raw));

        var reloaded = await _db.RefreshTokens.FindAsync(token.Id);
        Assert.Null(reloaded!.RevokedAt);
    }

    [Fact]
    public async Task LogoutAsync_UnknownToken_Throws()
    {
        var user = await SeedUserAsync();

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _sut.LogoutAsync(user.Id, "never-issued-token"));
    }

    [Fact]
    public async Task LogoutAsync_AlreadyRevokedOwnToken_SucceedsIdempotently()
    {
        var user = await SeedUserAsync();
        var (token, raw) = await SeedRefreshTokenAsync(user.Id, revokedAt: DateTime.UtcNow.AddDays(-1));

        await _sut.LogoutAsync(user.Id, raw);

        var reloaded = await _db.RefreshTokens.FindAsync(token.Id);
        Assert.NotNull(reloaded!.RevokedAt);
    }
}