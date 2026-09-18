using System.Collections.Generic;

namespace CardVault.Models;

/// <summary>
/// One labelled value inside a non-card entry. Masked fields are stored here too
/// (as plain values) — the template decides whether a label is reveal-gated.
/// </summary>
public sealed class EntryField
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// The sensitive payload of a non-card entry. Like <see cref="CardSecureData"/> it
/// exists only in memory after decryption and is never persisted in plaintext.
/// </summary>
public sealed class EntrySecureData
{
    public string Notes { get; set; } = string.Empty;
    public List<EntryField> Fields { get; set; } = new();
    public List<SecretEntry> Secrets { get; set; } = new();
}