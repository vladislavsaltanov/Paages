using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Domain.Exceptions;
using Paages.Infrastructure.Data;
using Paages.Infrastructure.Services;
using Paages.Tests.TestHelpers;

namespace Paages.Tests.Services;

public class PublicationServiceTests : IAsyncLifetime
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private SqliteConnection _connection = null!;
    private PaagesDbContext _db = null!;
    private PublicationService _sut = null!;

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
            Nickname = "TestUser",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _sut = new PublicationService(_db, new FakeCurrentUser(TestUserId));
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Note> SeedNoteAsync(string title, string content = "<p>Test</p>")
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            UserId = TestUserId,
            Title = title,
            ContentHtml = content,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        return note;
    }

    [Fact]
    public async Task PublishAsync_NewPublication_CreatesWithSlugAndReturnsIt()
    {
        var note = await SeedNoteAsync("My First Note");

        var publication = await _sut.PublishAsync(note.Id);

        Assert.NotNull(publication);
        Assert.Equal(note.Id, publication.NoteId);
        Assert.Equal("my-first-note", publication.Slug);
        Assert.Equal("TestUser", publication.AuthorNickname);
        Assert.Equal(note.ContentHtml, publication.ContentHtml);
        
        var inDb = await _db.Publications.FindAsync(publication.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task PublishAsync_NoteNotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.PublishAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task PublishAsync_AlreadyPublishedButNotRevoked_UpdatesContentButKeepsSlug()
    {
        var note = await SeedNoteAsync("Original Title", "<p>Old</p>");
        var firstPub = await _sut.PublishAsync(note.Id);
        var originalSlug = firstPub.Slug;

        note.Title = "New Title";
        note.ContentHtml = "<p>New</p>";
        await _db.SaveChangesAsync();

        var secondPub = await _sut.PublishAsync(note.Id);

        Assert.Equal(originalSlug, secondPub.Slug);
        Assert.Equal("New Title", secondPub.Title);
        Assert.Equal("<p>New</p>", secondPub.ContentHtml);
    }

    [Fact]
    public async Task PublishAsync_PreviouslyRevoked_CreatesNewSlugAndUnrevokes()
    {
        var note = await SeedNoteAsync("Test Note");
        var pub = await _sut.PublishAsync(note.Id);
        await _sut.RevokeAsync(note.Id);

        var republished = await _sut.PublishAsync(note.Id);

        Assert.Null(republished.RevokedAt);
        Assert.Equal("test-note", republished.Slug);
    }

    [Fact]
    public async Task RevokeAsync_ValidPublication_SetsRevokedAt()
    {
        var note = await SeedNoteAsync("Revoke Me");
        var pub = await _sut.PublishAsync(note.Id);

        await _sut.RevokeAsync(note.Id);

        var reloaded = await _db.Publications.FindAsync(pub.Id);
        Assert.NotNull(reloaded!.RevokedAt);
    }

    [Fact]
    public async Task RevokeAsync_PublicationNotFound_ThrowsNotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.RevokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetBySlugAsync_ValidSlug_ReturnsPublication()
    {
        var note = await SeedNoteAsync("Find Me");
        var pub = await _sut.PublishAsync(note.Id);

        var result = await _sut.GetBySlugAsync(pub.Slug);

        Assert.NotNull(result);
        Assert.Equal(pub.Id, result.Id);
    }

    [Fact]
    public async Task GetBySlugAsync_RevokedSlug_ReturnsNull()
    {
        var note = await SeedNoteAsync("Revoked");
        var pub = await _sut.PublishAsync(note.Id);
        await _sut.RevokeAsync(note.Id);

        var result = await _sut.GetBySlugAsync(pub.Slug);

        Assert.Null(result);
    }

    [Fact]
    public async Task PublishAsync_CyrillicTitle_TransliteratesCorrectly()
    {
        var note = await SeedNoteAsync("Заметка о C# 12");

        var pub = await _sut.PublishAsync(note.Id);

        Assert.Equal("zametka-o-c-12", pub.Slug);
    }

    [Fact]
    public async Task PublishAsync_EmptyOrInvalidTitle_FallsBackToNote()
    {
        var note = await SeedNoteAsync("??? !!!");

        var pub = await _sut.PublishAsync(note.Id);

        Assert.Equal("note", pub.Slug);
    }
}
