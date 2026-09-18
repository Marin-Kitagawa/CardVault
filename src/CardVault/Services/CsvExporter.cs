using System;
using System.Collections.Generic;
using System.Text;
using CardVault.Models;

namespace CardVault.Services;

/// <summary>
/// Plain-text CSV serialization of vault contents. Deliberately plain (not
/// encrypted) — the settings UI shows a warning before writing. One row per
/// key/value pair: name + kind identify every entry, so a spreadsheet can pivot
/// the rows back together. Secrets are included only because the user explicitly
/// chose a csv export; the dialog states this clearly.
/// </summary>
public static class CsvExporter
{
    public const string FileExtension = "csv";

    public static byte[] Export(
        IReadOnlyList<VaultEntry> entries,
        IReadOnlyList<object?> payloads)
    {
        var sb = new StringBuilder();
        sb.Append("Name,Kind,Field,Value\r\n");

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            var payload = payloads[i];

            sb.Append(Cell(e.Name)).Append(',').Append(Cell(EntryKinds.DisplayName(e.Kind))).Append(',').Append(Cell("Name")).Append(',').Append(Cell(e.Name)).Append("\r\n");

            if (payload is CardSecureData card)
            {
                AppendRow(sb, e, "Brand", e.Brand);
                AppendRow(sb, e, "Holder", card.Holder);
                AppendRow(sb, e, "Number", card.Number);
                if (card.ExpiryMonth.Length > 0)
                    AppendRow(sb, e, "Expiry", $"{card.ExpiryMonth}/{card.ExpiryYear}");
                AppendRow(sb, e, "CVV", card.Cvv);
                AppendRow(sb, e, "Notes", card.Notes);
                AppendSecrets(sb, e, card.Secrets);
            }
            else if (payload is EntrySecureData generic)
            {
                foreach (var f in generic.Fields)
                    AppendRow(sb, e, f.Label, f.Value);
                AppendRow(sb, e, "Notes", generic.Notes);
                AppendSecrets(sb, e, generic.Secrets);
            }
        }

        // UTF-8 with BOM so Excel/PowerQuery detect the encoding reliably.
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var withBom = new byte[3 + bytes.Length];
        bytes.CopyTo(withBom, 3);
        withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
        return withBom;
    }

    private static void AppendSecrets(StringBuilder sb, VaultEntry e, IReadOnlyList<SecretEntry> secrets)
    {
        foreach (var s in secrets ?? new List<SecretEntry>())
            AppendRow(sb, e, $"[secret] {s.Name}", s.Value);
    }

    private static void AppendRow(StringBuilder sb, VaultEntry e, string field, string value)
        => sb.Append(Cell(e.Name)).Append(',').Append(Cell(EntryKinds.DisplayName(e.Kind))).Append(',')
             .Append(Cell(field)).Append(',').Append(Cell(value)).Append("\r\n");

    private static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}