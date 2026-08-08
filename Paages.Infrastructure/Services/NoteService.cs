using Microsoft.EntityFrameworkCore;
using Paages.Domain.Entities;
using Paages.Infrastructure.Data;
using Paages.Domain.Interfaces;
namespace Paages.Infrastructure.Services;

public class NoteService(PaagesDbContext db, AppState appState, ITabsState tabsState, ICurrentUser currentUser)
{
    #region Get/Load
    public async Task<List<Folder>> GetFoldersAsync()
    {
        var userId = await currentUser.GetIdAsync();
        return await db.Folders.Where(f => f.UserId == userId).ToListAsync();
    }

    public async Task<List<Note>> GetNotesAsync()
    {
        var userId = await currentUser.GetIdAsync();
        return await db.Notes.Where(n => n.UserId == userId).ToListAsync();
    }

    public async Task<Folder?> GetFolderAsync(Guid id)
    {
        var userId = await currentUser.GetIdAsync();
        return await db.Folders.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
    }

    public async Task<Note?> GetNoteAsync(Guid id)
    {
        var userId = await currentUser.GetIdAsync();
        return await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    }
    public async Task<List<Folder>> GetFolderTreeAsync()
    {
        var userId = await currentUser.GetIdAsync();
        var allFolders = await db.Folders
                .Include(f => f.Notes)
                .Where(f => f.UserId == userId)
                .ToListAsync();

        foreach (var folder in allFolders)
        {
            folder.Children = allFolders.Where(f => f.ParentId == folder.Id).ToList();
        }

        return allFolders.Where(f => f.ParentId == null).ToList();
    }
    public async Task<HashSet<Guid>> GetAllDescendantFolderIdsAsync(Guid noteId)
    {
        var descendantIds = new HashSet<Guid>();

        var note = await GetNoteAsync(noteId);

        if (note is null || note.Folder is null)
            return descendantIds;

        var folder = note.Folder;
        while (folder is not null)
        {
            descendantIds.Add(folder.Id);
            folder = folder.Parent;
        }
        
        return descendantIds;
    }
    public async Task<List<ITreeNode>> GetPinnedItemsAsync()
    {
        var userId = await currentUser.GetIdAsync();
        var pinnedFolders = await db.Folders.Include(f => f.Notes).Where(f => f.UserId == userId && f.IsPinned).ToListAsync();
        var pinnedNotes = await db.Notes.Where(n => n.UserId == userId && n.IsPinned).ToListAsync();

        return pinnedFolders.Cast<ITreeNode>().Concat(pinnedNotes.Cast<ITreeNode>()).ToList();
    }

    public async Task<List<Note>> GetNotesWithoutFolderAsync()
    {
        var userId = await currentUser.GetIdAsync();
        return await db.Notes
            .Where(n => n.FolderId == null && n.UserId == userId)
            .OrderBy(n => n.SortOrder)
            .ToListAsync();
    }
    public async Task<List<Note>> GetNotesByIdsAsync(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        var userId = await currentUser.GetIdAsync();
        if (idList.Count == 0) return new List<Note>();
        return await db.Notes.Where(n => idList.Contains(n.Id) && n.UserId == userId).ToListAsync();
    }
    private async Task<List<ITreeNode>> LoadSiblingsAsync(Guid? parentId)
    {
        var userId = await currentUser.GetIdAsync();
        
        var folders = await db.Folders.Where(f => f.ParentId == parentId && f.UserId == userId).ToListAsync();
        var notes = await db.Notes.Where(n => n.FolderId == parentId && n.UserId == userId).ToListAsync();

        return folders.Cast<ITreeNode>()
            .Concat(notes.Cast<ITreeNode>())
            .OrderBy(n => n.SortOrder)
            .ToList();
    }
    public async Task<bool> HasFolder(Guid? guid)
    {
        if (guid is null) return false;

        var note = await FindNoteAsync(guid.Value);
        return note?.FolderId.HasValue ?? false;
    }
    #endregion
    #region Save
    public async Task SaveNoteContentAsync(Guid id, string html)
    {
        var note = await FindNoteAsync(id);
        if (note is null) return;

        note.ContentHtml = html;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    #endregion
    #region Create/Duplicate
    public async Task<Folder> CreateFolderAsync(Guid? parentId)
    {
        var userId = await currentUser.GetIdAsync();
        var siblings = await LoadSiblingsAsync(parentId);

        foreach (var sibling in siblings)
            sibling.SortOrder++;

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = "Новая папка",
            UserId = userId,
            ParentId = parentId,
            SortOrder = 0
        };

        db.Folders.Add(folder);
        await db.SaveChangesAsync();
        appState.NotifyTreeChanged();
        return folder;
    }
    public async Task<Note> CreateNoteAsync(Guid? folderId)
    {
        var userId = await currentUser.GetIdAsync();
        var siblings = await LoadSiblingsAsync(folderId);

        foreach (var sibling in siblings)
            sibling.SortOrder++;

        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = "Без названия",
            UserId = userId,
            ContentHtml = "<p></p>",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FolderId = folderId,
            SortOrder = 0
        };

        db.Notes.Add(note);
        await db.SaveChangesAsync();
        appState.NotifyTreeChanged();
        return note;
    }
    public async Task<Note> DuplicateNoteAsync(Guid id)
    {
        var userId = await currentUser.GetIdAsync();
        var source = await FindNoteAsync(id);
        if (source is null) throw new InvalidOperationException("Note not found.");

        var siblings = await LoadSiblingsAsync(source.FolderId);
        foreach (var sibling in siblings)
            sibling.SortOrder++;    

        var copy = new Note
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = $"{source.Title} (копия)".Truncate(100)!,
            ContentHtml = source.ContentHtml,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            FolderId = source.FolderId,
            SortOrder = 0
        };

        db.Notes.Add(copy);
        await db.SaveChangesAsync();
        appState.NotifyTreeChanged();
        return copy;
    }
    #endregion
    #region Delete
    public async Task DeleteNoteAsync(Guid id)
    {
        var note = await FindNoteAsync(id);
        if (note is null) return;

        await DeleteNodeAsync(note, note.FolderId, db.Notes);
        appState.NotifyTreeChanged();
    }

    public async Task DeleteFolderAsync(Guid id)
    {
        var folder = await FindFolderAsync(id);
        if (folder is null) return;

        await DeleteNodeAsync(folder, folder.ParentId, db.Folders);
        appState.NotifyTreeChanged();
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
    #region Move/Pin
    public async Task MoveAsync(Guid nodeId, bool isFolder, Guid? newParentId, Guid? insertBeforeId)
    {
        if (newParentId.HasValue)
        {
            var target = await FindFolderAsync(newParentId.Value);
            if (target is null)
                throw new InvalidOperationException("Target folder not found.");
        }

        // check if folder is a descendent of new parent folder via loop
        if (isFolder && newParentId.HasValue)
        {
            var parent = await FindFolderAsync(newParentId.Value);
            while (parent != null)
            {
                if (parent.Id == nodeId)
                    throw new InvalidOperationException("Cannot move a folder into one of its descendants.");

                if (parent.ParentId == null)
                    break;

                parent = await FindFolderAsync(parent.ParentId.Value);
            }
        }

        ITreeNode? node;
        Guid? oldParentId;

        if (isFolder)
        {
            var folder = await FindFolderAsync(nodeId);
            if (folder is null) return;
            oldParentId = folder.ParentId;
            folder.ParentId = newParentId;
            node = folder;
        }
        else
        {
            var note = await FindNoteAsync(nodeId);
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
        appState.NotifyTreeChanged();
    }
    public async Task TogglePinAsync(Guid nodeId, bool isFolder)
    {
        if (isFolder)
        {
            var folder = await FindFolderAsync(nodeId);
            if (folder is null) return;

            folder.IsPinned = !folder.IsPinned;
        }
        else
        {
            var note = await FindNoteAsync(nodeId);
            if (note is null) return;

            note.IsPinned = !note.IsPinned;
        }

        await db.SaveChangesAsync();
        appState.NotifyTreeChanged();
    }
    #endregion
    #region Rename
    public async Task<string> RenameNoteAsync(Guid id, string title)
    {
        var note = await FindNoteAsync(id);
        if (note is null) return title;

        note.Title = string.IsNullOrWhiteSpace(title) ? "Без названия" : title.Trim().Truncate(100)!;
        note.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        appState.NotifyTreeChanged();
        appState.NotifyNoteRenamed(note.Id, note.Title);
        return note.Title ?? "Без названия";
    }
    public async Task<string> RenameFolderAsync(Guid id, string name)
    {
        var folder = await FindFolderAsync(id);
        if (folder is null) return name;

        folder.Name = string.IsNullOrWhiteSpace(name) ? "Новая папка" : name.Trim().Truncate(100)!;
        await db.SaveChangesAsync();
        appState.NotifyTreeChanged();
        return folder.Name;
    }
    #endregion
    #region Find
    private async Task<Note?> FindNoteAsync(Guid id)
    {
        var userId = await currentUser.GetIdAsync();
        return await db.Notes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
    }
    private async Task<Folder?> FindFolderAsync(Guid id)
    {
        var userId = await currentUser.GetIdAsync();
        return await db.Folders.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId);
    }
    #endregion
    #region Misc
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