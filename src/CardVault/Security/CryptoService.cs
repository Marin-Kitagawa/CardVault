using System;
using System.Security.Cryptography;
using System.Text;

namespace CardVault.Security;

/// <summary>
/// All cryptographic primitives for the vault.
///  - Key derivation: PBKDF2-HMAC-SHA256, 310k iterations.
///  - Authenticated encryption: AES-256-GCM (random 96-bit nonce per message).
/// </summary>
public static class CryptoService
{
    public const int KdfIterations = 310_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;

    private static readonly byte[] SealContext = Encoding.UTF8.GetBytes("CardVault::seal::v1");

    public static byte[] RandomBytes(int count)
    {
        var b = new byte[count];
        RandomNumberGenerator.Fill(b);
        return b;
    }

    public static byte[] DeriveKey(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySize);

    /// <summary>Deterministic hash used to verify the master password without exposing the key.</summary>
    public static byte[] ComputeSealHash(byte[] masterKey)
        => SHA256.HashData(Concat(SealContext, masterKey));

    /// <summary>Encrypts plain into nonce|tag|ciphertext.</summary>
    public static byte[] Encrypt(byte[] key, ReadOnlySpan<byte> plain, string aad = "")
    {
        var nonce = RandomBytes(NonceSize);
        var tag = new byte[TagSize];
        var cipher = new byte[plain.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plain, cipher, tag, ToAad(aad));

        var result = new byte[NonceSize + TagSize + plain.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, NonceSize);
        cipher.CopyTo(result, NonceSize + TagSize);
        CryptographicOperations.ZeroMemory(nonce);
        return result;
    }

    public static byte[] Decrypt(byte[] key, ReadOnlySpan<byte> blob, string aad = "")
    {
        if (blob.Length < NonceSize + TagSize)
            throw new CryptographicException("Corrupted encrypted payload.");

        var nonce = blob[..NonceSize];
        var tag = blob.Slice(NonceSize, TagSize);
        var cipher = blob[(NonceSize + TagSize)..];
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain, ToAad(aad));
        return plain;
    }

    public static bool SlowEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => CryptographicOperations.FixedTimeEquals(left, right);

    private static byte[]? ToAad(string aad)
        => string.IsNullOrEmpty(aad) ? null : Encoding.UTF8.GetBytes(aad);

    private static byte[] Concat(byte[] first, byte[] second)
    {
        var r = new byte[first.Length + second.Length];
        first.CopyTo(r, 0);
        second.CopyTo(r, first.Length);
        return r;
    }
}