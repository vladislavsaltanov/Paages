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

    public async Task MoveAsync(Guid nodeId, bool isFolder, Guid? newParentId, Guid? insertBeforeId)
    {
        // check if folder is a descendent of new parent folder via loop
        if (isFolder && newParentId.HasValue)
        {
            var parent = await _db.Folders.FindAsync(newParentId.Value);
            while (parent != null)
            {
                if (parent.Id == nodeId)
                    throw new InvalidOperationException("Cannot move a folder into one of its descendants.");

                if (parent.ParentId == null)
                    break;

                parent = await _db.Folders.FindAsync(parent.ParentId);
            }
        }

        ITreeNode? node;
        Guid? oldParentId;

        if (isFolder)
        {
            var folder = await _db.Folders.FindAsync(nodeId);
            if (folder is null) return;
            oldParentId = folder.ParentId;
            folder.ParentId = newParentId;
            node = folder;
        }
        else
        {
            var note = await _db.Notes.FindAsync(nodeId);
            if (note is null) return;
            oldParentId = note.FolderId;
            note.FolderId = newParentId;
            node = note;
        }

        // insert into new parent's sibling list at the requested position
        var newSiblings = await LoadSiblingsAsync(newParentId);
        newSiblings = newSiblings.Where(s => s.Id != nodeId).ToList();

        int index = insertBeforeId is null ? -1 : newSiblings.FindIndex(s => s.Id == insertBeforeId);

        if (index == -1)
            newSiblings.Add(node); // no target or target not found - append at the end
        else
            newSiblings.Insert(index, node);

        for (int i = 0; i < newSiblings.Count; i++)
            newSiblings[i].SortOrder = i;

        // close the gap left in the old parent's sibling list, if it moved out
        if (oldParentId != newParentId)
        {
            var oldSiblings = await LoadSiblingsAsync(oldParentId);
            oldSiblings = oldSiblings.Where(s => s.Id != nodeId).ToList();

            for (int i = 0; i < oldSiblings.Count; i++)
                oldSiblings[i].SortOrder = i;
        }

        await _db.SaveChangesAsync();
    }

    private async Task<List<ITreeNode>> LoadSiblingsAsync(Guid? parentId)
    {
        var folders = await _db.Folders.Where(f => f.ParentId == parentId).ToListAsync();
        var notes = await _db.Notes.Where(n => n.FolderId == parentId).ToListAsync();

        return folders.Cast<ITreeNode>()
            .Concat(notes.Cast<ITreeNode>())
            .OrderBy(n => n.SortOrder)
            .ToList();
    }

    public async Task<List<Note>> GetNotesByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return new List<Note>();
        return await _db.Notes.Where(n => idList.Contains(n.Id)).ToListAsync();
    }

    public async Task SeedTestDataAsync()
    {
        // if (await _db.Notes.AnyAsync()) return;

        //var home = new Folder { Name = "Дом", Id = Guid.NewGuid() };
        //var archive = new Folder { Name = "Архив", Id = Guid.NewGuid(), ParentId = home.Id };

        //_db.Folders.AddRange(home, archive);

        _db.Notes.AddRange(
            new Note { Id = Guid.NewGuid(), Title = "Заметка без папки 5", ContentHtml = "<p>Текст 1</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Note { Id = Guid.NewGuid(), Title = "Заметка без папки 6", ContentHtml = "<p>Текст 2</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Доме 1", ContentHtml = "<p>Текст 3</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = home.Id },
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Доме 2", ContentHtml = "<p>Текст 4</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = home.Id },
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Архиве 1", ContentHtml = "<p>Текст 5</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = archive.Id },
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Архиве 2", ContentHtml = "<p>Текст 6</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = archive.Id }
        );

        await _db.SaveChangesAsync();
    }
}