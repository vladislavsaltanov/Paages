using Paages.Domain.Entities;
using Paages.Domain.Interfaces;
using Paages.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Paages.Domain.Exceptions;
using System.Text;
using System.Text.RegularExpressions;

namespace Paages.Infrastructure.Services;

public class PublicationService(PaagesDbContext db, ICurrentUser currentUser)
{
    public async Task<Publication> PublishAsync(Guid noteId)
    {
        var userId = await currentUser.GetIdAsync();
        var note = await db.Notes.SingleOrDefaultAsync(n => n.Id == noteId && n.UserId == userId)
            ?? throw new NotFoundException("Note not found.");
        var user = await db.Users.SingleAsync(u => u.Id == userId);

        var publication = await db.Publications.SingleOrDefaultAsync(p => p.NoteId == noteId);

        if (publication is null)
        {
            publication = new Publication
            {
                Id = Guid.NewGuid(),
                NoteId = noteId,
                UserId = userId,
                Slug = await GenerateSlugAsync(note.Title),
                PublishedAt = DateTime.UtcNow
            };
            db.Publications.Add(publication);
        }
        else if (publication.RevokedAt is not null)
        {
            publication.Slug = await GenerateSlugAsync(note.Title, publication.Id);
        }

        publication.Title = note.Title;
        publication.ContentHtml = note.ContentHtml ?? "";
        publication.AuthorNickname = user.Nickname;
        publication.UpdatedAt = DateTime.UtcNow;
        publication.RevokedAt = null;

        await db.SaveChangesAsync();
        return publication;
    }

    public async Task RevokeAsync(Guid noteId)
    {
        var userId = await currentUser.GetIdAsync();
        var publication = await db.Publications.SingleOrDefaultAsync(p => p.NoteId == noteId && p.UserId == userId)
            ?? throw new NotFoundException("Publication not found.");

        publication.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public Task<Publication?> GetBySlugAsync(string slug) =>
        db.Publications.SingleOrDefaultAsync(p => p.Slug == slug && p.RevokedAt == null);

    private async Task<string> GenerateSlugAsync(string title, Guid? excludePublicationId = null)
    {
        var baseSlug = Slugify(title);
        var slug = baseSlug;
        var suffix = 2;
        while (await db.Publications.AnyAsync(p => p.Slug == slug && p.Id != excludePublicationId))
            slug = $"{baseSlug}-{suffix++}";
        return slug;
    }

    private static readonly Dictionary<char, string> CyrillicMap = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d",
        ['е'] = "e", ['ё'] = "yo", ['ж'] = "zh", ['з'] = "z", ['и'] = "i",
        ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
        ['э'] = "e", ['ю'] = "yu", ['я'] = "ya"
    };

    private static string Slugify(string title)
    {
        var sb = new StringBuilder();
        foreach (var ch in title.ToLowerInvariant())
        {
            if (CyrillicMap.TryGetValue(ch, out var latin))
                sb.Append(latin);
            else if (char.IsLetterOrDigit(ch) && ch < 128)
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch is '-' or '_')
                sb.Append('-');
        }

        var slug = Regex.Replace(sb.ToString(), "-{2,}", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? "note" : slug;
    }
}