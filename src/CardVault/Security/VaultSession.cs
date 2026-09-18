using System;
using System.Security.Cryptography;

namespace CardVault.Security;

/// <summary>
/// Holds the decrypted vault master key in memory. Zeroed on lock.
/// </summary>
public sealed class VaultSession
{
    public byte[]? Key { get; private set; }
    public bool IsUnlocked => Key is not null;

    public event Action? Locked;
    public event Action? Unlocked;

    public void Open(byte[] key)
    {
        if (Key is not null) Lock();
        Key = key;
        Unlocked?.Invoke();
    }

    public void Lock()
    {
        if (Key is not null) CryptographicOperations.ZeroMemory(Key);
        Key = null;
        Locked?.Invoke();
    }
}