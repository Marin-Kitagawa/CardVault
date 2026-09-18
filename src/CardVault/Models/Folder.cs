using System;

namespace CardVault.Models;

/// <summary>
/// A user-defined folder used to group entries into a nested, iconed tree.
/// ParentId is empty for a root-level folder.
/// </summary>
public sealed class Folder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ParentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "folder";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}