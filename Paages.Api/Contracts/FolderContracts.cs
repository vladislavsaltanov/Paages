using System.ComponentModel.DataAnnotations;

namespace Paages.Api.Contracts;

public record FolderResponse(Guid Id, string Name, Guid? ParentId, bool IsPinned, int SortOrder);

public record CreateFolderRequest(Guid? ParentId);

public record RenameFolderRequest([property: Required, MinLength(1)] string Name);