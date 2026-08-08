using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Infrastructure.Data;
using Paages.Infrastructure.Services;
using Paages.Tests.TestHelpers;

namespace Paages.Tests.Services;

public class NoteServiceTests : IAsyncLifetime
{
    private static readonly Guid TestUserId = Guid.NewGuid();

    private SqliteConnection _connection = null!;
    private PaagesDbContext _db = null!;
    private NoteService _sut = null!;

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

        _sut = new NoteService(_db, new AppState(), new FakeTabsState(), new FakeCurrentUser(TestUserId));
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateNoteAsync_NoFolder_CreatesRootNoteWithDefaults()
    {
        var note = await _sut.CreateNoteAsync(null);

        Assert.Null(note.FolderId);
        Assert.Equal("Без названия", note.Title);
        Assert.Equal(0, note.SortOrder);
        Assert.Equal(TestUserId, note.UserId);
    }

    [Fact]
    public async Task CreateNoteAsync_ExistingSiblings_ShiftsTheirSortOrder()
    {
        var first = await _sut.CreateNoteAsync(null);
        var second = await _sut.CreateNoteAsync(null);

        var reloadedFirst = await _db.Notes.FindAsync(first.Id);

        Assert.Equal(0, second.SortOrder);
        Assert.Equal(1, reloadedFirst!.SortOrder);
    }

    [Fact]
    public async Task CreateNoteAsync_WithFolderId_AssignsToThatFolder()
    {
        var folder = await _sut.CreateFolderAsync(null);

        var note = await _sut.CreateNoteAsync(folder.Id);

        Assert.Equal(folder.Id, note.FolderId);
    }

    [Fact]
    public async Task RenameNoteAsync_ValidTitle_UpdatesAndReturnsIt()
    {
        var note = await _sut.CreateNoteAsync(null);

        var result = await _sut.RenameNoteAsync(note.Id, "Идеи для проекта");

        Assert.Equal("Идеи для проекта", result);
        var reloaded = await _db.Notes.FindAsync(note.Id);
        Assert.Equal("Идеи для проекта", reloaded!.Title);
    }

    [Fact]
    public async Task RenameNoteAsync_WhitespaceTitle_FallsBackToDefault()
    {
        var note = await _sut.CreateNoteAsync(null);

        var result = await _sut.RenameNoteAsync(note.Id, "   ");

        Assert.Equal("Без названия", result);
    }
    
    [Fact]
    public async Task RenameNoteAsync_Truncate()
    {
        var note = await _sut.CreateNoteAsync(null);

        var result = await _sut.RenameNoteAsync(note.Id, "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since 1966,");

        Assert.Equal("Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the …", result);
    }

    [Fact]
    public async Task RenameNoteAsync_NoteNotFound_ReturnsInputTitleUnchanged()
    {
        var result = await _sut.RenameNoteAsync(Guid.NewGuid(), "неважно");

        Assert.Equal("неважно", result);
    }

    [Fact]
    public async Task RenameNoteAsync_OverLengthTitle_TruncatesTo100CharsPlusEllipsis()
    {
        var note = await _sut.CreateNoteAsync(null);
        var longTitle = new string('a', 150);

        var result = await _sut.RenameNoteAsync(note.Id, longTitle);

        Assert.Equal(101, result.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public async Task MoveAsync_ReorderWithinSameParent_UpdatesSortOrder()
    {
        var first = await _sut.CreateNoteAsync(null);
        var second = await _sut.CreateNoteAsync(null);

        await _sut.MoveAsync(first.Id, isFolder: false, newParentId: null, insertBeforeId: second.Id);

        var reloadedFirst = await _db.Notes.FindAsync(first.Id);
        var reloadedSecond = await _db.Notes.FindAsync(second.Id);
        Assert.Equal(0, reloadedFirst!.SortOrder);
        Assert.Equal(1, reloadedSecond!.SortOrder);
    }

    [Fact]
    public async Task MoveAsync_ToDifferentFolder_ReparentsAndClosesGapInOldParent()
    {
        var folderA = await _sut.CreateFolderAsync(null);
        var folderB = await _sut.CreateFolderAsync(null);
        var noteInA1 = await _sut.CreateNoteAsync(folderA.Id);
        var noteInA2 = await _sut.CreateNoteAsync(folderA.Id);

        await _sut.MoveAsync(noteInA2.Id, isFolder: false, newParentId: folderB.Id, insertBeforeId: null);

        var moved = await _db.Notes.FindAsync(noteInA2.Id);
        var remainingInA = await _db.Notes.FindAsync(noteInA1.Id);
        Assert.Equal(folderB.Id, moved!.FolderId);
        Assert.Equal(0, remainingInA!.SortOrder);
    }

    [Fact]
    public async Task MoveAsync_ToRoot_ClearsFolderId()
    {
        var folder = await _sut.CreateFolderAsync(null);
        var note = await _sut.CreateNoteAsync(folder.Id);

        await _sut.MoveAsync(note.Id, isFolder: false, newParentId: null, insertBeforeId: null);

        var reloaded = await _db.Notes.FindAsync(note.Id);
        Assert.Null(reloaded!.FolderId);
    }

    [Fact]
    public async Task MoveAsync_FolderIntoOwnDescendant_ThrowsInvalidOperationException()
    {
        var parent = await _sut.CreateFolderAsync(null);
        var child = await _sut.CreateFolderAsync(parent.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.MoveAsync(parent.Id, isFolder: true, newParentId: child.Id, insertBeforeId: null));
    }

    [Fact]
    public async Task MoveAsync_InsertBeforeIdNotFound_AppendsAtEnd()
    {
        var first = await _sut.CreateNoteAsync(null);
        var second = await _sut.CreateNoteAsync(null);

        await _sut.MoveAsync(second.Id, isFolder: false, newParentId: null, insertBeforeId: Guid.NewGuid());

        var reloadedSecond = await _db.Notes.FindAsync(second.Id);
        Assert.Equal(1, reloadedSecond!.SortOrder);
    }

    [Fact]
    public async Task MoveAsync_TargetFolderNotFound_ThrowsInvalidOperationException()
    {
        var note = await _sut.CreateNoteAsync(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.MoveAsync(note.Id, isFolder: false, newParentId: Guid.NewGuid(), insertBeforeId: null));
    }
}