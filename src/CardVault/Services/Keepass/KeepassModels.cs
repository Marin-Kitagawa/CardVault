using System.Collections.Generic;

namespace CardVault.Services.Keepass;

/// <summary>Parsed structure of a KeePass 2 (.kdbx) database.</summary>
public sealed class KeepassFile
{
    public KeepassGroup Root { get; set; } = new();
}

/// <summary>One KeePass group — maps to a CardVault folder.</summary>
public sealed class KeepassGroup
{
    public string Name { get; set; } = string.Empty;
    public List<KeepassGroup> Groups { get; } = new();
    public List<KeepassEntry> Entries { get; } = new();
}

/// <summary>One KeePass entry/record — maps to a CardVault entry.</summary>
public sealed class KeepassEntry
{
    public string Title { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    /// <summary>Non-standard key/value string attributes in file order.</summary>
    public List<KeepassField> Fields { get; } = new();
}

public sealed record KeepassField(string Name, string Value);