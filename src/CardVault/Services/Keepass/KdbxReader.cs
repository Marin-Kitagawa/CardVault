using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using CardVault.Security;

namespace CardVault.Services.Keepass;

/// <summary>Raised when a file is not a readable KeePass KDBX 3.x database.</summary>
public sealed class KeepassFormatException : Exception
{
    public KeepassFormatException(string message) : base(message) { }
}

/// <summary>
/// Reads KeePass 2 KDBX 3.1 password-protected databases.
///
/// Pipeline (mirrors KeePassXC's Kdbx3Reader):
/// header fields (1-byte id + 2-byte little-endian length, closed by an
/// EndOfHeader field carrying "\r\n\r\n") → AES-KDF key derivation
/// → AES-256-CBC decryption → 32-byte stream-start check → hashed block stream
/// → optional gzip → XML with a Salsa20-protected inner random stream.
/// </summary>
public static class KdbxReader
{
    private const uint Signature1 = 0x9AA2D903;
    private const uint Signature2 = 0xB54BFB67;
    private const int VersionMajor3 = 0x0003;

    private static readonly byte[] AesCipherUuid =
    {
        0x31, 0xC1, 0xF2, 0xE6, 0xBF, 0x71, 0x43, 0x50,
        0xBE, 0x58, 0x05, 0x21, 0x6A, 0xFC, 0x5A, 0xFF,
    };

    internal static readonly byte[] InnerStreamSalsa20Iv = { 0xE8, 0x30, 0x09, 0x4B, 0x97, 0x20, 0x5D, 0x2A };

    private enum HeaderField
    {
        End = 0,
        Comment = 1,
        CipherId = 2,
        CompressionFlags = 3,
        MasterSeed = 4,
        TransformSeed = 5,
        TransformRounds = 6,
        EncryptionIv = 7,
        ProtectedStreamKey = 8,
        StreamStartBytes = 9,
        InnerRandomStreamId = 10,
    }

    private readonly record struct Header(
        uint Version,
        int Compression,
        byte[] MasterSeed,
        byte[] TransformSeed,
        ulong TransformRounds,
        byte[] EncryptionIv,
        byte[] ProtectedStreamKey,
        byte[] StreamStartBytes,
        int HeaderByteLength);

    /// <summary>Parse a KDBX 3.x file into its group/entry tree. Throws <see cref="KeepassFormatException"/> on bad input.</summary>
    public static KeepassFile Read(byte[] file, string password, bool verifyHeaderHash = true)
    {
        if (file is null || file.Length < 12) throw new KeepassFormatException("The file is too small to be a KeePass database.");

        var header = ParseHeader(file);
        var finalKey = DeriveKey(password, header);

        try
        {
            byte[] payload;
            try
            {
                payload = DecryptPayload(file, header.HeaderByteLength, header.EncryptionIv, finalKey);
            }
            catch (CryptographicException)
            {
                throw new KeepassFormatException("Wrong password or corrupted file.");
            }

            if (!payload.AsSpan(0, 32).SequenceEqual(header.StreamStartBytes))
                throw new KeepassFormatException("Wrong password or corrupted file.");

            var blocks = ReadHashedBlocks(payload.AsSpan(32));
            byte[] xmlBytes;
            if (header.Compression == 1)
            {
                using var input = new MemoryStream(blocks);
                using var gzip = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gzip.CopyTo(output);
                xmlBytes = output.ToArray();
            }
            else
            {
                xmlBytes = blocks;
            }

            return ParseXml(xmlBytes, header.ProtectedStreamKey, file, header.HeaderByteLength, verifyHeaderHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(finalKey);
        }
    }

    // ------------------------------ header ------------------------------

    private static Header ParseHeader(byte[] file)
    {
        using var ms = new MemoryStream(file, false);
        using var br = new BinaryReader(ms);

        uint ReadLeUInt32() => (uint)(br.ReadByte() | br.ReadByte() << 8 | br.ReadByte() << 16 | br.ReadByte() << 24);

        if (ReadLeUInt32() != Signature1 || ReadLeUInt32() != Signature2)
            throw new KeepassFormatException("This is not a KeePass 2 database file.");

        var version = ReadLeUInt32();
        if ((version >> 16) != VersionMajor3)
            throw new KeepassFormatException($"Unsupported KDBX version {version:X8}. Only KeePass KDBX 3.x files can be imported.");

        byte[]? cipher = null, masterSeed = null, transformSeed = null, encryptionIv = null;
        byte[]? protectedStreamKey = null, streamStartBytes = null;
        ulong? transformRounds = null;
        var compression = 0;
        var innerRandomStreamId = -1;

        while (true)
        {
            var id = br.ReadByte();
            var length = (int)(br.ReadByte() | br.ReadByte() << 8);
            var data = br.ReadBytes(length);
            if (data.Length != length)
                throw new KeepassFormatException("Truncated header field in KeePass file.");
            if (id == (byte)HeaderField.End) break;

            switch ((HeaderField)id)
            {
                case HeaderField.CipherId: cipher = data; break;
                case HeaderField.CompressionFlags:
                    compression = (int)(data[0] | (data.Length > 1 ? data[1] << 8 : 0)
                        | (data.Length > 2 ? data[2] << 16 : 0) | (data.Length > 3 ? data[3] << 24 : 0));
                    break;
                case HeaderField.MasterSeed: masterSeed = data; break;
                case HeaderField.TransformSeed: transformSeed = data; break;
                case HeaderField.TransformRounds:
                    transformRounds = 0;
                    for (var i = 0; i < data.Length && i < 8; i++) transformRounds |= (ulong)data[i] << (8 * i);
                    break;
                case HeaderField.EncryptionIv: encryptionIv = data; break;
                case HeaderField.ProtectedStreamKey: protectedStreamKey = data; break;
                case HeaderField.StreamStartBytes: streamStartBytes = data; break;
                case HeaderField.InnerRandomStreamId: innerRandomStreamId = (int)data[0]; break;
            }
        }

        if (!AesCipherUuid.SequenceEqual(cipher ?? Array.Empty<byte>()))
            throw new KeepassFormatException("This KeePass file uses an unsupported cipher (only AES-256 is supported).");
        if (innerRandomStreamId != 2)
            throw new KeepassFormatException("This KeePass file uses an unsupported inner stream cipher (only Salsa20 is supported).");
        if (masterSeed?.Length != 32 || transformSeed?.Length != 32 || encryptionIv?.Length != 16
            || protectedStreamKey?.Length != 32 || streamStartBytes?.Length != 32)
            throw new KeepassFormatException("Invalid or missing KeePass header fields.");
        if (transformRounds is null)
            throw new KeepassFormatException("Missing transform rounds in KeePass header.");

        return new Header(version, compression, masterSeed, transformSeed, transformRounds.Value,
            encryptionIv, protectedStreamKey, streamStartBytes, (int)ms.Position);
    }

    // ------------------------------ keys & decryption ------------------------------

    private static byte[] DeriveKey(string password, Header header)
    {
        // PasswordKey::rawKey = SHA256(UTF8(password)); CompositeKey::rawKey = SHA256(concat) → same width for one component.
        var passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        var composite = SHA256.HashData(passwordHash);
        CryptographicOperations.ZeroMemory(passwordHash);

        var transformed = AesKdfTransform(composite, header.TransformSeed, header.TransformRounds);
        CryptographicOperations.ZeroMemory(composite);

        var finalKey = SHA256.HashData(Concat(header.MasterSeed, transformed));
        CryptographicOperations.ZeroMemory(transformed);
        return finalKey;
    }

    /// <summary>AES-KDF: encrypt the 32-byte composite key with AES-256-ECB using the transform seed, <c>rounds</c> times, then SHA-256.</summary>
    private static byte[] AesKdfTransform(byte[] data, byte[] transformSeed, ulong rounds)
    {
        var buffer = (byte[])data.Clone();
        using var aes = Aes.Create();
        aes.Key = transformSeed;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        for (ulong i = 0; i < rounds; i++)
            encryptor.TransformBlock(buffer, 0, buffer.Length, buffer, 0);
        var result = SHA256.HashData(buffer);
        CryptographicOperations.ZeroMemory(buffer);
        return result;
    }

    private static byte[] DecryptPayload(byte[] file, int start, byte[] iv, byte[] key)
    {
        using var input = new MemoryStream(file, start, file.Length - start, false);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        using var cipher = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
        using var output = new MemoryStream();
        cipher.CopyTo(output);
        return output.ToArray();
    }

    // ------------------------------ hashed blocks ------------------------------

    private static byte[] ReadHashedBlocks(ReadOnlySpan<byte> data)
    {
        using var output = new MemoryStream();
        var pos = 0;
        uint expected = 0;

        while (true)
        {
            if (pos + 8 > data.Length)
                throw new KeepassFormatException("Truncated payload in KeePass file.");

            var index = ReadLeUInt32(data, pos);
            pos += 4;
            if (index != expected)
                throw new KeepassFormatException("Corrupted block index in KeePass file.");

            if (pos + 32 > data.Length)
                throw new KeepassFormatException("Truncated payload in KeePass file.");
            var hash = data.Slice(pos, 32);
            pos += 32;

            var blockSize = ReadLeInt32(data, pos);
            pos += 4;
            if (blockSize == 0)
            {
                if (hash.SequenceEqual(new byte[32]))
                    return output.ToArray();
                throw new KeepassFormatException("Invalid final block in KeePass file.");
            }
            if (blockSize < 0 || pos + blockSize > data.Length)
                throw new KeepassFormatException("Invalid block size in KeePass file.");

            var block = data.Slice(pos, blockSize);
            pos += blockSize;

            if (!hash.SequenceEqual(SHA256.HashData(block)))
                throw new KeepassFormatException("Block hash mismatch in KeePass file (file is corrupt or the password is wrong).");

            output.Write(block);
            expected++;
        }
    }

    private static uint ReadLeUInt32(ReadOnlySpan<byte> s, int p)
        => (uint)(s[p] | s[p + 1] << 8 | s[p + 2] << 16 | s[p + 3] << 24);

    private static int ReadLeInt32(ReadOnlySpan<byte> s, int p)
        => s[p] | s[p + 1] << 8 | s[p + 2] << 16 | s[p + 3] << 24;

    // ------------------------------ xml ------------------------------

    private static KeepassFile ParseXml(byte[] xmlBytes, byte[] protectedStreamKey,
        byte[] file, int headerLength, bool verifyHeaderHash)
    {
        var protectedValues = new List<(XElement Element, byte[] Data)>();

        XDocument doc;
        using (var stream = new MemoryStream(xmlBytes, false))
        using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit }))
        {
            doc = XDocument.Load(reader, LoadOptions.None);
        }

        foreach (var element in doc.Descendants())
        {
            if (element.Name.LocalName != "Value" || !IsTrue(element.Attribute("Protected")))
                continue;

            var text = element.Value?.Trim() ?? string.Empty;
            if (text.Length == 0) continue;

            try
            {
                protectedValues.Add((element, Convert.FromBase64String(text)));
            }
            catch (FormatException)
            {
                throw new KeepassFormatException("Invalid base64 protected value in KeePass XML.");
            }
        }

        if (protectedValues.Count > 0)
        {
            var innerKey = SHA256.HashData(protectedStreamKey);
            var salsa = new Salsa20(innerKey, InnerStreamSalsa20Iv);
            foreach (var (element, data) in protectedValues)
            {
                var plain = new byte[data.Length];
                salsa.Transcode(data, plain);
                element.Value = Encoding.UTF8.GetString(plain);
                CryptographicOperations.ZeroMemory(plain);
            }
            CryptographicOperations.ZeroMemory(innerKey);
        }

        var headerHashNode = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "HeaderHash");
        if (verifyHeaderHash && headerHashNode is not null)
        {
            byte[] stored;
            try { stored = Convert.FromBase64String(headerHashNode.Value?.Trim() ?? string.Empty); }
            catch (FormatException) { stored = Array.Empty<byte>(); }

            if (stored.Length == 32 && !stored.SequenceEqual(SHA256.HashData(file.AsSpan(0, headerLength))))
                throw new KeepassFormatException("KeePass header hash mismatch — file has been tampered with.");
        }

        return new KeepassFile
        {
            Root = ParseGroup(doc.Root?.Element("Root")?.Element("Group"))
        };
    }

    private static KeepassGroup ParseGroup(XElement? groupElement)
    {
        var group = new KeepassGroup();
        if (groupElement is null) return group;

        var nameElement = groupElement.Element("Name");
        group.Name = nameElement?.Value ?? string.Empty;

        foreach (var entryElement in groupElement.Elements("Entry"))
            group.Entries.Add(ParseEntry(entryElement));

        foreach (var subElement in groupElement.Elements("Group"))
            group.Groups.Add(ParseGroup(subElement));

        return group;
    }

    private static KeepassEntry ParseEntry(XElement entryElement)
    {
        var entry = new KeepassEntry();
        foreach (var stringElement in entryElement.Elements("String"))
        {
            var key = stringElement.Element("Key")?.Value ?? string.Empty;
            var value = stringElement.Element("Value")?.Value ?? string.Empty;
            switch (key)
            {
                case "Title": entry.Title = value; break;
                case "UserName": entry.UserName = value; break;
                case "Password": entry.Password = value; break;
                case "URL": entry.Url = value; break;
                case "Notes": entry.Notes = value; break;
                default:
                    if (key.Length > 0)
                        entry.Fields.Add(new KeepassField(key, value));
                    break;
            }
        }
        return entry;
    }

    private static bool IsTrue(XAttribute? attr)
        => attr is not null && string.Equals(attr.Value, "True", StringComparison.OrdinalIgnoreCase);

    private static byte[] Concat(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result, 0);
        second.CopyTo(result, first.Length);
        return result;
    }
}