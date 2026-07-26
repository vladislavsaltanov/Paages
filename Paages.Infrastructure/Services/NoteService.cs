using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Infrastructure.Data;

namespace Paages.Infrastructure.Services;

public class NoteService
{
    private readonly PaagesDbContext _db;

    public NoteService(PaagesDbContext db)
    {
        _db = db;
    }

    public async Task<List<Folder>> GetFoldersAsync()
    {
        return await _db.Folders.ToListAsync();
    }

    public async Task<List<Note>> GetNotesAsync()
    {
        return await _db.Notes.ToListAsync();
    }

    public async Task<Note?> GetNoteAsync(Guid id)
    {
        return await _db.Notes.FindAsync(id);
    }

    public async Task SaveNoteContentAsync(Guid id, string html)
    {
        var note = await _db.Notes.FindAsync(id);
        if (note is null) return;

        note.ContentHtml = html;
        note.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }


    public async Task<List<Folder>> GetFolderTreeAsync()
    {
        var allFolders = await _db.Folders
                .Include(f => f.Notes)
                .ToListAsync();

        foreach (var folder in allFolders)
        {
            folder.Children = allFolders.Where(f => f.ParentId == folder.Id).ToList();
        }

        return allFolders.Where(f => f.ParentId == null).ToList();
    }

    public async Task<List<Note>> GetNotesWithoutFolderAsync()
    {
        return await _db.Notes
            .Where(n => n.FolderId == null)
            .OrderBy(n => n.SortOrder)
            .ToListAsync();
    }

    public async Task SeedTestDataAsync()
    {
        if (await _db.Notes.AnyAsync()) return;

        var home = new Folder { Name = "Дом", Id = Guid.NewGuid() };
        var archive = new Folder { Name = "Архив", Id = Guid.NewGuid(), ParentId = home.Id };

        _db.Folders.AddRange(home, archive);

        _db.Notes.AddRange(
            new Note { Id = Guid.NewGuid(), Title = "Заметка без папки 1", ContentHtml = "<p>Текст 1</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Note { Id = Guid.NewGuid(), Title = "Заметка без папки 2", ContentHtml = "<p>Текст 2</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Note { Id = Guid.NewGuid(), Title = "Заметка в Доме 1", ContentHtml = "<p>Текст 3</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = home.Id },
            new Note { Id = Guid.NewGuid(), Title = "Заметка в Доме 2", ContentHtml = "<p>Текст 4</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = home.Id },
            new Note { Id = Guid.NewGuid(), Title = "Заметка в Архиве 1", ContentHtml = "<p>Текст 5</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = archive.Id },
            new Note { Id = Guid.NewGuid(), Title = "Заметка в Архиве 2", ContentHtml = "<p>Текст 6</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = archive.Id }
        );

        await _db.SaveChangesAsync();
    }
}