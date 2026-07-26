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
}