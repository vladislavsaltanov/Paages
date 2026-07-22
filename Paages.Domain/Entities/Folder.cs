using Paages.Domain.Enums;

namespace Paages.Domain.Entities;

public class Folder
{
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
}