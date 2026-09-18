namespace CardVault.Models;

using System.Collections.Generic;

/// <summary>
/// A single named secret entry stored on a card (PIN, security question,
/// 2FA backup code, online login, ...). Lives only inside the encrypted payload.
/// </summary>
public sealed class SecretEntry
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// The sensitive card payload. Exists only in memory after decryption.
/// Never persisted in plaintext.
/// </summary>
public sealed class CardSecureData
{
    public string Holder { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;     // digits only
    public string ExpiryMonth { get; set; } = string.Empty; // "12"
    public string ExpiryYear { get; set; } = string.Empty;  // "28"
    public string Cvv { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<SecretEntry> Secrets { get; set; } = new();
}