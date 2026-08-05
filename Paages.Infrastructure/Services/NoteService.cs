using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Infrastructure.Data;
using Paages.Domain.Interfaces;
namespace Paages.Infrastructure.Services;

public class NoteService(PaagesDbContext db, AppState appState, ITabsState tabsState)
{
    #region Get/Load
    public async Task<List<Folder>> GetFoldersAsync()
    {
        return await db.Folders.ToListAsync();
    }

    public async Task<List<Note>> GetNotesAsync()
    {
        return await db.Notes.ToListAsync();
    }

    public async Task<Note?> GetNoteAsync(Guid id)
    {
        return await db.Notes.FindAsync(id);
    }
    public async Task<List<Folder>> GetFolderTreeAsync()
    {
        var allFolders = await db.Folders
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
        return await db.Notes
            .Where(n => n.FolderId == null)
            .OrderBy(n => n.SortOrder)
            .ToListAsync();
    }
    public async Task<List<Note>> GetNotesByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return new List<Note>();
        return await db.Notes.Where(n => idList.Contains(n.Id)).ToListAsync();
    }
    private async Task<List<ITreeNode>> LoadSiblingsAsync(Guid? parentId)
    {
        var folders = await db.Folders.Where(f => f.ParentId == parentId).ToListAsync();
        var notes = await db.Notes.Where(n => n.FolderId == parentId).ToListAsync();

        return folders.Cast<ITreeNode>()
            .Concat(notes.Cast<ITreeNode>())
            .OrderBy(n => n.SortOrder)
            .ToList();
    }
    #endregion
    #region Save
    public async Task SaveNoteContentAsync(Guid id, string html)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null) return;

        note.ContentHtml = html;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    #endregion
    #region Create/Duplicate
    public async Task<Folder> CreateFolderAsync(Guid? parentId)
    {
        var siblings = await LoadSiblingsAsync(parentId);

        foreach (var sibling in siblings)
            sibling.SortOrder++;

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "Новая папка",
            ParentId = parentId,
            SortOrder = 0
        };

        db.Folders.Add(folder);
        await db.SaveChangesAsync();
        return folder;
    }
    public async Task<Note> CreateNoteAsync(Guid? folderId)
    {
         var siblings = await LoadSiblingsAsync(folderId);

        foreach (var sibling in siblings)
            sibling.SortOrder++;

        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Без названия",
            ContentHtml = "<p></p>",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FolderId = folderId,
            SortOrder = 0
        };

        db.Notes.Add(note);
        await db.SaveChangesAsync();
        return note;
    }
    public async Task<Note> DuplicateNoteAsync(Guid id)
    {
        var source = await db.Notes.FindAsync(id);
        if (source is null) throw new InvalidOperationException("Note not found.");

        var siblings = await LoadSiblingsAsync(source.FolderId);
        foreach (var sibling in siblings)
            sibling.SortOrder++;    

        var copy = new Note
        {
            Id = Guid.NewGuid(),
            Title = $"{source.Title} (копия)".Truncate(100)!,
            ContentHtml = source.ContentHtml,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FolderId = source.FolderId,
            SortOrder = 0
        };

        db.Notes.Add(copy);
        await db.SaveChangesAsync();
        return copy;
    }
    #endregion
    #region Delete
    public async Task DeleteNoteAsync(Guid id)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null) return;

        await DeleteNodeAsync(note, note.FolderId, db.Notes);
    }

    public async Task DeleteFolderAsync(Guid id)
    {
        var folder = await db.Folders.FindAsync(id);
        if (folder is null) return;

        await DeleteNodeAsync(folder, folder.ParentId, db.Folders);
    }
    private async Task DeleteNodeAsync<T>(T node, Guid? parentId, DbSet<T> set) where T: class, ITreeNode
    {
        var siblings = await LoadSiblingsAsync(parentId);

        set.Remove(node);
        Reindex(siblings.Where(s => s.Id != node.Id));

        await db.SaveChangesAsync();
        appState.NotifyTreeChanged();

        try
        {
            if (node is Note note)
                tabsState.Close(note.Id);
        } 
        catch { /* there wasnt an open tab for this note, so nothing to close */ }
    }
    #endregion
    #region Move/Pin/Rename
    public async Task MoveAsync(Guid nodeId, bool isFolder, Guid? newParentId, Guid? insertBeforeId)
    {
        // check if folder is a descendent of new parent folder via loop
        if (isFolder && newParentId.HasValue)
        {
            var parent = await db.Folders.FindAsync(newParentId.Value);
            while (parent != null)
            {
                if (parent.Id == nodeId)
                    throw new InvalidOperationException("Cannot move a folder into one of its descendants.");

                if (parent.ParentId == null)
                    break;

                parent = await db.Folders.FindAsync(parent.ParentId);
            }
        }

        ITreeNode? node;
        Guid? oldParentId;

        if (isFolder)
        {
            var folder = await db.Folders.FindAsync(nodeId);
            if (folder is null) return;
            oldParentId = folder.ParentId;
            folder.ParentId = newParentId;
            node = folder;
        }
        else
        {
            var note = await db.Notes.FindAsync(nodeId);
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

        await db.SaveChangesAsync();
    }
    public async Task TogglePinAsync(Guid nodeId, bool isFolder)
    {
        if (isFolder)
        {
            var folder = await db.Folders.FindAsync(nodeId);
            if (folder is null) return;

            folder.IsPinned = !folder.IsPinned;
        }
        else
        {
            var note = await db.Notes.FindAsync(nodeId);
            if (note is null) return;

            note.IsPinned = !note.IsPinned;
        }

        await db.SaveChangesAsync();
    }
    public async Task<string> RenameNoteAsync(Guid id, string title)
    {
        var note = await db.Notes.FindAsync(id);
        if (note is null) return title;

        note.Title = string.IsNullOrWhiteSpace(title) ? "Без названия" : title.Trim().Truncate(100)!;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return note.Title ?? "Без названия";
    }
    #endregion

    #region Miscellaneous
    public async Task SeedTestDataAsync()
    {
        // if (await db.Notes.AnyAsync()) return;

        //var home = new Folder { Name = "Дом", Id = Guid.NewGuid() };
        //var archive = new Folder { Name = "Архив", Id = Guid.NewGuid(), ParentId = home.Id };

        //db.Folders.AddRange(home, archive);

        db.Notes.AddRange(
            new Note { Id = Guid.NewGuid(), Title = "Заметка без папки 5", ContentHtml = "<p>Текст 1</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new Note { Id = Guid.NewGuid(), Title = "Заметка без папки 6", ContentHtml = "<p>Текст 2</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Доме 1", ContentHtml = "<p>Текст 3</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = home.Id },
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Доме 2", ContentHtml = "<p>Текст 4</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = home.Id },
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Архиве 1", ContentHtml = "<p>Текст 5</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = archive.Id },
            // new Note { Id = Guid.NewGuid(), Title = "Заметка в Архиве 2", ContentHtml = "<p>Текст 6</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, FolderId = archive.Id }
        );

        await db.SaveChangesAsync();
    }
    private void Reindex(IEnumerable<ITreeNode> siblings)
    {
        var list = siblings.ToList();
        for (int i = 0; i < list.Count; i++)
            list[i].SortOrder = i;
    }
    #endregion
}

public static class StringExt
{
    public static string? Truncate(this string? value, int maxLength, string truncationSuffix = "…")
    {
        return value?.Length > maxLength
            ? value.Substring(0, maxLength) + truncationSuffix
            : value;
    }
}