using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CardVault.Models;

namespace CardVault.Services;

/// <summary>
/// Reads back the plain-text CSV written by <see cref="CsvExporter"/>. Rows are
/// grouped by (name, kind) in document order; every written value is restored to
/// its place — card fields, generic field/value pairs, notes, and secret entries.
/// Unknown or empty rows are skipped so spreadsheets stay forgiving.
/// </summary>
public static class CsvImporter
{
    /// <summary>Parsed CSV rows, grouped per entry in file order.</summary>
    public static List<ImportedEntry> Parse(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var rows = ParseRows(text);

        var groups = new List<CsvEntry>();
        foreach (var row in rows)
        {
            if (row.Length < 4) continue;
            var title = row[0].Trim();
            var kind = row[1].Trim();
            if (title.Length == 0) continue;

            var open = groups.LastOrDefault();
            if (open is not null && open.Name == title && open.KindText == kind)
                open.Rows.Add(row);
            else
                groups.Add(new CsvEntry(title, kind) { Rows = { row } });
        }

        var imported = new List<ImportedEntry>();
        foreach (var group in groups)
        {
            if (!TryDisplayName(group.KindText, out var kind)) continue;

            var entry = new VaultEntry { Name = group.Name, Kind = kind, Brand = "generic", Accent = -1 };
            imported.Add(kind == EntryKind.Card
                ? new ImportedEntry(entry, BuildCard(entry, group.Rows))
                : new ImportedEntry(entry, BuildGeneric(entry, group.Rows)));
        }

        return imported;
    }

    private static CardSecureData BuildCard(VaultEntry entry, List<string[]> rows)
    {
        var data = new CardSecureData();
        foreach (var row in rows)
        {
            var field = row[2].Trim();
            var value = row[3];

            switch (field)
            {
                case "Name": break;
                case "Brand": entry.Brand = value.Trim(); break;
                case "Holder": data.Holder = value; break;
                case "Number": data.Number = new string(value.Where(char.IsDigit).ToArray()); break;
                case "Expiry":
                    var parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length >= 1) data.ExpiryMonth = parts[0].PadLeft(2, '0');
                    if (parts.Length >= 2) data.ExpiryYear = parts[1].PadLeft(2, '0')[..Math.Min(2, parts[1].PadLeft(2, '0').Length)];
                    break;
                case "CVV": data.Cvv = new string(value.Where(char.IsDigit).ToArray()); break;
                case "Notes": data.Notes = value; break;
                default: AddAsSecret(data.Secrets, field, value); break;
            }
        }

        return data;
    }

    private static EntrySecureData BuildGeneric(VaultEntry entry, List<string[]> rows)
    {
        var data = new EntrySecureData();
        foreach (var row in rows)
        {
            var field = row[2].Trim();
            var value = row[3];

            if (field == "Name") continue;
            if (field == "Notes") { data.Notes = value; continue; }

            if (field.StartsWith("[secret]", StringComparison.OrdinalIgnoreCase))
                data.Secrets.Add(new SecretEntry
                {
                    Name = field.Length > "[secret]".Length ? field["[secret]".Length..].Trim() : string.Empty,
                    Value = value,
                });
            else
                data.Fields.Add(new EntryField { Label = field, Value = value });
        }

        return data;
    }

    private static void AddAsSecret(ICollection<SecretEntry> secrets, string name, string value)
    {
        if (name.StartsWith("[secret]", StringComparison.OrdinalIgnoreCase))
            name = name.Length > "[secret]".Length ? name["[secret]".Length..].Trim() : string.Empty;
        secrets.Add(new SecretEntry { Name = name, Value = value });
    }

    private static bool TryDisplayName(string text, out EntryKind kind)
    {
        var match = EntryKinds.All.FirstOrDefault(k =>
            string.Equals(k.DisplayName, text.Trim(), StringComparison.OrdinalIgnoreCase));
        kind = match?.Kind ?? EntryKind.Note;
        return match is not null;
    }

    /// <summary>Standard quoted CSV row parser; handles commas, quotes and newlines in cells.</summary>
    internal static List<string[]> ParseRows(string text)
    {
        var rows = new List<string[]>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else inQuotes = false;
                }
                else cell.Append(c);
            }
            else
            {
                switch (c)
                {
                    case '"': inQuotes = true; break;
                    case ',':
                        cells.Add(cell.ToString());
                        cell.Clear();
                        break;
                    case '\n':
                        cells.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(cells.ToArray());
                        cells.Clear();
                        break;
                    case '\r': break;
                    default: cell.Append(c); break;
                }
            }
        }

        if (cell.Length > 0 || cells.Count > 0)
        {
            cells.Add(cell.ToString());
            rows.Add(cells.ToArray());
        }

        return rows;
    }

    private sealed class CsvEntry
    {
        public CsvEntry(string name, string kindText)
        {
            Name = name;
            KindText = kindText;
        }

        public string Name { get; }
        public string KindText { get; }
        public List<string[]> Rows { get; } = new();
    }
}