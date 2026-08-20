using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Infrastructure.Data;
using Paages.Infrastructure.Services;

namespace Paages.Tests.Services;

public class ApiTokenServiceTests : IAsyncLifetime
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private SqliteConnection _connection = null!;
    private PaagesDbContext _db = null!;
    private ApiTokenService _sut = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PaagesDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new PaagesDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _db.Users.Add(new User
        {
            Id = TestUserId,
            Email = "test@paages.dev",
            PasswordHash = "unused",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _sut = new ApiTokenService(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task IssueAsync_CreatesTokenAndReturnsRawString()
    {
        var (token, rawToken) = await _sut.IssueAsync(TestUserId, "Test Token");

        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        Assert.Equal("Test Token", token.Name);
        Assert.Equal(TestUserId, token.UserId);
        Assert.NotNull(token.TokenHash);
        
        var inDb = await _db.ApiTokens.FindAsync(token.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task ValidateAsync_ValidRawToken_ReturnsUserAndUpdatesLastUsedAt()
    {
        var (token, rawToken) = await _sut.IssueAsync(TestUserId, "Test Token");
        Assert.Null(token.LastUsedAt);

        var user = await _sut.ValidateAsync(rawToken);

        Assert.NotNull(user);
        Assert.Equal(TestUserId, user.Id);
        
        var inDb = await _db.ApiTokens.FindAsync(token.Id);
        Assert.NotNull(inDb!.LastUsedAt);
    }

    [Fact]
    public async Task ValidateAsync_InvalidRawToken_ReturnsNull()
    {
        var user = await _sut.ValidateAsync("invalid_token");

        Assert.Null(user);
    }

    [Fact]
    public async Task ValidateAsync_RevokedToken_ReturnsNull()
    {
        var (token, rawToken) = await _sut.IssueAsync(TestUserId, "Test Token");
        await _sut.RevokeAsync(TestUserId, token.Id);
        _db.ChangeTracker.Clear();

        var user = await _sut.ValidateAsync(rawToken);

        Assert.Null(user);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredToken_ReturnsNull()
    {
        var (token, rawToken) = await _sut.IssueAsync(TestUserId, "Test Token");
        token.ExpiresAt = DateTime.UtcNow.AddMinutes(-10);
        await _db.SaveChangesAsync();

        var user = await _sut.ValidateAsync(rawToken);

        Assert.Null(user);
    }

    [Fact]
    public async Task RevokeAsync_RevokesOnlyTargetToken()
    {
        var (token1, _) = await _sut.IssueAsync(TestUserId, "Token 1");
        var (token2, _) = await _sut.IssueAsync(TestUserId, "Token 2");

        await _sut.RevokeAsync(TestUserId, token1.Id);
        _db.ChangeTracker.Clear();

        var reloaded1 = await _db.ApiTokens.FindAsync(token1.Id);
        var reloaded2 = await _db.ApiTokens.FindAsync(token2.Id);
        
        Assert.NotNull(reloaded1!.RevokedAt);
        Assert.Null(reloaded2!.RevokedAt);
    }

    [Fact]
    public async Task RevokeAllAsync_RevokesAllTokensForUser()
    {
        var (token1, _) = await _sut.IssueAsync(TestUserId, "Token 1");
        var (token2, _) = await _sut.IssueAsync(TestUserId, "Token 2");

        await _sut.RevokeAllAsync(TestUserId);
        _db.ChangeTracker.Clear();

        var reloaded1 = await _db.ApiTokens.FindAsync(token1.Id);
        var reloaded2 = await _db.ApiTokens.FindAsync(token2.Id);
        
        Assert.NotNull(reloaded1!.RevokedAt);
        Assert.NotNull(reloaded2!.RevokedAt);
    }

    [Fact]
    public async Task ListAsync_ReturnsAllUserTokensOrderedByCreatedAtDescending()
    {
        await _sut.IssueAsync(TestUserId, "First");
        await Task.Delay(100);
        await _sut.IssueAsync(TestUserId, "Second");

        var tokens = await _sut.ListAsync(TestUserId);

        Assert.Equal(2, tokens.Count);
        Assert.Equal("Second", tokens[0].Name);
        Assert.Equal("First", tokens[1].Name);
    }
}
