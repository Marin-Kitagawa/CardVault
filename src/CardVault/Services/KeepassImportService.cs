using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CardVault.Data;
using CardVault.Models;
using CardVault.Services.Keepass;

namespace CardVault.Services;

public sealed record KeepassImportResult(int Entries, int Folders);

/// <summary>
/// Imports a KeePass KDBX 3.x database into the vault. Groups become nested
/// folders (reusing the folder tree), entries become cards/logins/documents/notes
/// with their protected values decrypted and stored in the encrypted payload.
/// </summary>
public sealed class KeepassImportService
{
    private readonly VaultDatabase _db;

    public KeepassImportService(VaultDatabase db) => _db = db;

    public KeepassImportResult Import(byte[] file, string password)
    {
        var db = KdbxReader.Read(file, password);
        var folderIds = new Dictionary<KeepassGroup, string>();

        int ImportGroup(KeepassGroup group, string parentId)
        {
            var folder = _db.CreateFolder(group.Name, "folder", parentId);
            folderIds[group] = folder.Id;
            var count = 0;
            foreach (var child in group.Groups)
                count += ImportGroup(child, folder.Id);
            foreach (var entry in group.Entries)
            {
                Save(_db, group, folder.Id, entry);
                count++;
            }
            return count;
        }

        var entryCount = 0;
        foreach (var group in db.Root.Groups)
            entryCount += ImportGroup(group, string.Empty);
        foreach (var entry in db.Root.Entries)
        {
            Save(_db, db.Root, string.Empty, entry);
            entryCount++;
        }

        return new KeepassImportResult(entryCount, folderIds.Count);
    }

    private static void Save(VaultDatabase db, KeepassGroup group, string folderId, KeepassEntry source)
    {
        var name = string.IsNullOrWhiteSpace(source.Title)
            ? "Imported entry"
            : source.Title;
        var accent = -1;
        var entry = new VaultEntry
        {
            Name = name,
            FolderId = folderId,
        };

        if (TryCard(source, out var card))
        {
            var digits = new string((card.Number ?? string.Empty).Where(char.IsDigit).ToArray());
            var brand = CardBrandInfo.Detect(digits).ToString().ToLowerInvariant();

            var data = new CardSecureData
            {
                Holder = card.Holder,
                Number = digits,
                ExpiryMonth = card.ExpiryMonth,
                ExpiryYear = card.ExpiryYear,
                Cvv = card.Cvv,
                Notes = source.Notes,
                Secrets = RemainingSecrets(source, card),
            };

            db.SaveImported(entry, name, EntryKind.Card, brand, accent, data, null);
            return;
        }

        var hasLogin = source.Password.Length > 0 || source.UserName.Length > 0 || source.Url.Length > 0;
        EntryKind kind = hasLogin ? EntryKind.Login : source.Fields.Count > 0 ? EntryKind.Document : EntryKind.Note;

        var secrets = new List<SecretEntry>();
        if (source.Password.Length > 0)
            secrets.Add(new SecretEntry { Name = "Password", Value = source.Password });
        foreach (var field in source.Fields)
            secrets.Add(new SecretEntry { Name = field.Name, Value = field.Value });

        var dataGeneric = new EntrySecureData
        {
            Notes = source.Notes,
            Fields = LoginFields(source),
            Secrets = secrets,
        };

        db.SaveImported(entry, name, kind, string.Empty, accent, null, dataGeneric);
    }

    private static List<EntryField> LoginFields(KeepassEntry source)
    {
        var fields = new List<EntryField>(2);
        if (source.UserName.Length > 0)
            fields.Add(new EntryField { Label = "Username", Value = source.UserName });
        if (source.Url.Length > 0)
            fields.Add(new EntryField { Label = "Website or app", Value = source.Url });
        return fields;
    }

    // ------------------------------ card detection ------------------------------

    /// <summary>
    /// A KeePass entry without a dedicated card type stores cards as custom
    /// attributes ("Card Number", "CVV", "Expiry", "Cardholder", ...). Detect
    /// that shape and lift the values into the native card payload.
    /// </summary>
    private static bool TryCard(KeepassEntry entry, out CardPayload card)
    {
        card = default;
        string? number = null, expiry = null, cvv = null, holder = null;

        foreach (var field in entry.Fields)
        {
            var label = field.Name.Trim();
            if (label.Length == 0 || field.Value.Length == 0) continue;

            if (IsCardNumberLabel(label) && number is null) number = field.Value;
            else if (IsCvvLabel(label) && cvv is null) cvv = field.Value;
            else if (IsExpiryLabel(label) && expiry is null) expiry = field.Value;
            else if (IsHolderLabel(label) && holder is null) holder = field.Value;
        }

        if (string.IsNullOrWhiteSpace(number)) return false;

        var digits = new string(number.Where(char.IsDigit).ToArray());
        if (digits.Length < 12) return false;

        var expires = !string.IsNullOrWhiteSpace(expiry);
        if (!expires && string.IsNullOrWhiteSpace(cvv)) return false;

        var (month, year) = ParseExpiry(expiry ?? string.Empty);
        card = new CardPayload
        {
            Holder = holder ?? string.Empty,
            Number = digits,
            ExpiryMonth = month,
            ExpiryYear = year,
            Cvv = cvv ?? string.Empty,
        };
        return true;
    }

    private static bool IsCardNumberLabel(string label)
    {
        var l = Strip(label);
        if (l.Contains("number") && (l.Contains("card") || l.Contains("cc"))) return true;
        return l is "number" or "cardno" or "ccnumber" or "cardnum" or "card#";
    }

    private static bool IsCvvLabel(string label)
    {
        var l = Strip(label);
        return l is "cvv" or "cvc" or "cvv2" or "cvc2" or "ccv" or "security code";
    }

    private static bool IsExpiryLabel(string label)
    {
        var l = Strip(label);
        return l.Contains("expir") || l is "exp" or "valid till" or "expires";
    }

    private static bool IsHolderLabel(string label)
    {
        var l = Strip(label);
        return l.Contains("holder") || l is "name" or "card name" or "party";
    }

    private static string Strip(string s)
        => Regex.Replace(s.ToLowerInvariant().Trim(), "[^a-z0-9]", string.Empty);

    private static (string month, string year2) ParseExpiry(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return (string.Empty, string.Empty);

        var month = digits[..2];
        if (int.TryParse(month, out var m) && m is >= 1 and <= 12)
            return (month, digits[^2..]);
        return (string.Empty, string.Empty);
    }

    /// <summary>Secrets for a card: keep any straggler custom attributes as reveal-gated rows.</summary>
    private static List<SecretEntry> RemainingSecrets(KeepassEntry source, CardPayload card)
    {
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in source.Fields)
        {
            if (IsCardNumberLabel(field.Name) || IsCvvLabel(field.Name)
                || IsExpiryLabel(field.Name) || IsHolderLabel(field.Name))
                consumed.Add(field.Name);
        }

        var secrets = source.Password.Length > 0
            ? new List<SecretEntry> { new() { Name = "Password", Value = source.Password } }
            : new List<SecretEntry>();

        foreach (var field in source.Fields)
        {
            if (consumed.Contains(field.Name) || field.Value.Length == 0) continue;
            secrets.Add(new SecretEntry { Name = field.Name, Value = field.Value });
        }
        return secrets;
    }

    private static EntryField? MaybeField(string label, string value)
        => value.Length == 0 ? null : new EntryField { Label = label, Value = value };

    private struct CardPayload
    {
        public string Holder;
        public string Number;
        public string ExpiryMonth;
        public string ExpiryYear;
        public string Cvv;
    }
}