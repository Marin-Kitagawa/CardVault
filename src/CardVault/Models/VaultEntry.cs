using System;

namespace CardVault.Models;

/// <summary>
/// Non-sensitive metadata plus the encrypted payload blob. Stored in SQLite.
/// Blob layout: nonce(12) | auth tag(16) | ciphertext(JSON of
/// <see cref="CardSecureData"/> for cards, <see cref="EntrySecureData"/> otherwise).
/// </summary>
public sealed class VaultEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public EntryKind Kind { get; set; } = EntryKind.Card;
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = "generic";
    public int Accent { get; set; } = -1;
    public byte[] SecureBlob { get; set; } = Array.Empty<byte>();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}