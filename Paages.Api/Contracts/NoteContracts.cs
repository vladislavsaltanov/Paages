using System.ComponentModel.DataAnnotations;

namespace Paages.Api.Contracts;

public record NoteResponse(Guid Id, string Title, string? ContentHtml, Guid? FolderId,
    bool IsPinned, int SortOrder, DateTime CreatedAt, DateTime UpdatedAt);

public record CreateNoteRequest(Guid? FolderId);

public record RenameNoteRequest([property: Required, MinLength(1), MaxLength(100)] string Title);

public record MoveRequest(Guid? NewParentId, Guid? InsertBeforeId);

public record SaveContentRequest([property: Required] string ContentHtml);