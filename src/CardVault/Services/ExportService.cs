using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using CardVault.Models;
using CardVault.Security;

namespace CardVault.Services;

/// <summary>
/// Passphrase-encrypted export / import. The export file is fully self-contained:
/// card data is only reachable with the export passphrase (AES-256-GCM), and the
/// envelope authenticates against tampering.
/// </summary>
public sealed class ExportService
{
    public const string FileExtension = "cvault";
    private const string Magic = "CVLT";
    private const int FormatVersion = 2;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Build a byte[] export for the given entries. The payload list must be
    /// index-aligned: <c>null</c> for a card payload, a <see cref="CardSecureData"/>
    /// for cards, and an <see cref="EntrySecureData"/> for every other kind.
    /// </summary>
    public byte[] Export(
        IReadOnlyList<VaultEntry> entries,
        IReadOnlyList<object?> payloads,
        string passphrase)
    {
        var salt = CryptoService.RandomBytes(16);
        var key = CryptoService.DeriveKey(passphrase, salt, CryptoService.KdfIterations);

        var payload = new ExportPayload { ExportedAt = DateTimeOffset.UtcNow.ToString("O") };
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var p = payloads[i];
            var card = p as CardSecureData;
            var generic = p as EntrySecureData;

            payload.Items.Add(new ExportEntry
            {
                Id = e.Id,
                Kind = e.Kind.ToString().ToLowerInvariant(),
                Name = e.Name,
                Brand = e.Brand,
                Accent = e.Accent,
                Tags = e.Tags,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                // card fields
                Holder = card?.Holder ?? string.Empty,
                Number = card?.Number ?? string.Empty,
                ExpiryMonth = card?.ExpiryMonth ?? string.Empty,
                ExpiryYear = card?.ExpiryYear ?? string.Empty,
                Cvv = card?.Cvv ?? string.Empty,
                // shared
                Notes = card?.Notes ?? generic?.Notes ?? string.Empty,
                Secrets = (card?.Secrets ?? generic?.Secrets)?.Select(s => new ExportSecret { Name = s.Name, Value = s.Value }).ToList() ?? new(),
                // generic fields
                Fields = generic?.Fields?.Select(f => new ExportField { Label = f.Label, Value = f.Value }).ToList() ?? new(),
            });
        }

        var plain = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var blob = CryptoService.Encrypt(key, plain, Magic);
        CryptographicOperations.ZeroMemory(plain);

        var envelope = new ExportEnvelope
        {
            Magic = Magic,
            Version = FormatVersion,
            KdfSalt = Convert.ToBase64String(salt),
            KdfIterations = CryptoService.KdfIterations,
            Blob = Convert.ToBase64String(blob),
        };

        CryptographicOperations.ZeroMemory(salt);
        Array.Clear(key, 0, key.Length);

        return JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
    }

    /// <summary>
    /// Export encrypted with an already-derived key (used by unattended auto-backup
    /// and sync, where no passphrase prompt is possible). The envelope records the
    /// KDF salt and iteration count that produced <paramref name="key"/>, so a
    /// normal passphrase import reproduces the same key and can decrypt the file.
    /// </summary>
    public byte[] ExportWithKey(
        IReadOnlyList<VaultEntry> entries,
        IReadOnlyList<object?> payloads,
        byte[] key,
        byte[] salt,
        int iterations)
    {
        var payload = new ExportPayload { ExportedAt = DateTimeOffset.UtcNow.ToString("O") };
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var p = payloads[i];
            var card = p as CardSecureData;
            var generic = p as EntrySecureData;

            payload.Items.Add(new ExportEntry
            {
                Id = e.Id,
                Kind = e.Kind.ToString().ToLowerInvariant(),
                Name = e.Name,
                Brand = e.Brand,
                Accent = e.Accent,
                Tags = e.Tags,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt,
                Holder = card?.Holder ?? string.Empty,
                Number = card?.Number ?? string.Empty,
                ExpiryMonth = card?.ExpiryMonth ?? string.Empty,
                ExpiryYear = card?.ExpiryYear ?? string.Empty,
                Cvv = card?.Cvv ?? string.Empty,
                Notes = card?.Notes ?? generic?.Notes ?? string.Empty,
                Secrets = (card?.Secrets ?? generic?.Secrets)?.Select(s => new ExportSecret { Name = s.Name, Value = s.Value }).ToList() ?? new(),
                Fields = generic?.Fields?.Select(f => new ExportField { Label = f.Label, Value = f.Value }).ToList() ?? new(),
            });
        }

        var plain = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        var blob = CryptoService.Encrypt(key, plain, Magic);
        CryptographicOperations.ZeroMemory(plain);

        var envelope = new ExportEnvelope
        {
            Magic = Magic,
            Version = FormatVersion,
            KdfSalt = Convert.ToBase64String(salt),
            KdfIterations = iterations,
            Blob = Convert.ToBase64String(blob),
        };

        return JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
    }

    public List<ImportedEntry> Import(byte[] file, string passphrase)
    {
        var envelope = JsonSerializer.Deserialize<ExportEnvelope>(file, JsonOptions)
            ?? throw new InvalidDataException("This file is not a CardVault export.");

        if (envelope.Magic != Magic || envelope.Version is < 1 or > FormatVersion)
            throw new InvalidDataException("This export file uses an unsupported format version.");

        var salt = Convert.FromBase64String(envelope.KdfSalt ?? string.Empty);
        var key = CryptoService.DeriveKey(passphrase, salt, envelope.KdfIterations);
        var blob = Convert.FromBase64String(envelope.Blob ?? string.Empty);

        byte[] plain;
        try
        {
            plain = CryptoService.Decrypt(key, blob, Magic);
        }
        catch
        {
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidDataException("Wrong passphrase or corrupted file. Nothing was imported.");
        }

        var payload = JsonSerializer.Deserialize<ExportPayload>(plain, JsonOptions)
            ?? throw new InvalidDataException("Corrupted export contents.");

        var result = new List<ImportedEntry>();
        var items = payload.Items.Count > 0 ? payload.Items : (payload.Cards ?? new List<ExportEntry>());
        foreach (var c in items)
        {
            if (!Enum.TryParse<EntryKind>(c.Kind, true, out var kind))
                kind = EntryKind.Card;

            var record = new VaultEntry
            {
                Id = c.Id,
                Kind = kind,
                Name = c.Name,
                Brand = c.Brand ?? string.Empty,
                Accent = c.Accent,
                Tags = c.Tags ?? string.Empty,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
            };

            if (kind == EntryKind.Card)
            {
                result.Add(new ImportedEntry(record, new CardSecureData
                {
                    Holder = c.Holder,
                    Number = c.Number,
                    ExpiryMonth = c.ExpiryMonth,
                    ExpiryYear = c.ExpiryYear,
                    Cvv = c.Cvv,
                    Notes = c.Notes,
                    Secrets = c.Secrets?.Select(s => new SecretEntry { Name = s.Name, Value = s.Value }).ToList() ?? new(),
                }));
            }
            else
            {
                result.Add(new ImportedEntry(record, new EntrySecureData
                {
                    Notes = c.Notes,
                    Fields = c.Fields?.Select(f => new EntryField { Label = f.Label, Value = f.Value }).ToList() ?? new(),
                    Secrets = c.Secrets?.Select(s => new SecretEntry { Name = s.Name, Value = s.Value }).ToList() ?? new(),
                }));
            }
        }

        CryptographicOperations.ZeroMemory(plain);
        Array.Clear(key, 0, key.Length);
        return result;
    }
}

public sealed record ImportedEntry(VaultEntry Entry, object Payload);

internal sealed class ExportEnvelope
{
    public string Magic { get; set; } = string.Empty;
    public int Version { get; set; }
    public string KdfSalt { get; set; } = string.Empty;
    public int KdfIterations { get; set; }
    public string Blob { get; set; } = string.Empty;
}

internal sealed class ExportPayload
{
    public string ExportedAt { get; set; } = string.Empty;
    public List<ExportEntry> Items { get; set; } = new();

    /// <summary>Legacy v1 shape (cards only).</summary>
    public List<ExportEntry>? Cards { get; set; }
}

internal sealed class ExportEntry
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "card";
    public string Name { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int Accent { get; set; } = -1;
    public string Tags { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Holder { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string ExpiryMonth { get; set; } = string.Empty;
    public string ExpiryYear { get; set; } = string.Empty;
    public string Cvv { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public List<ExportField>? Fields { get; set; }
    public List<ExportSecret>? Secrets { get; set; }
}

internal sealed class ExportField
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

internal sealed class ExportSecret
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}