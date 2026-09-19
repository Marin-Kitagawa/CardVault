using System;
using System.Linq;
using System.Text;
using CardVault.Models;
using CardVault.Services;
using Xunit;

namespace CardVault.Tests;

public class CsvImportTests
{
    [Fact]
    public void RoundTrip_RestoresKindsFieldsAndSecrets()
    {
        var entries = new[]
        {
            new VaultEntry { Name = "WiFi Café", Kind = EntryKind.Login },
            new VaultEntry { Name = "Platinum", Kind = EntryKind.Card, Brand = "visa", Accent = 3 },
            new VaultEntry { Name = "Insurance", Kind = EntryKind.Document },
            new VaultEntry { Name = "Safe combo", Kind = EntryKind.Physical },
        };
        var payloads = new object?[]
        {
            new EntrySecureData
            {
                Notes = "Rooftop",
                Fields = { new EntryField { Label = "Username", Value = "ana", }, new EntryField { Label = "Password", Value = "s3cret" } },
                Secrets = { new SecretEntry { Name = "PIN", Value = "1234" } },
            },
            new CardSecureData
            {
                Holder = "ANA S",
                Number = "4111111111111111",
                ExpiryMonth = "09",
                ExpiryYear = "28",
                Cvv = "123",
                Notes = "Primary",
            },
            new EntrySecureData
            {
                Fields = { new EntryField { Label = "Policy number", Value = "P-100" } },
                Secrets = { new SecretEntry { Name = "Replacement code", Value = "R-9" } },
            },
            new EntrySecureData
            {
                Fields = { new EntryField { Label = "Combination", Value = "7-14-21" } },
            },
        };

        var bytes = CsvExporter.Export(entries, payloads);
        var imported = CsvImporter.Parse(bytes);

        Assert.Equal(entries.Length, imported.Count);

        var login = imported.First(i => i.Entry.Name == "WiFi Café");
        var ld = Assert.IsType<EntrySecureData>(login.Payload);
        Assert.Equal("ana", ld.Fields.First(f => f.Label == "Username").Value);
        Assert.Equal("s3cret", ld.Fields.First(f => f.Label == "Password").Value);
        Assert.Equal("Rooftop", ld.Notes);
        Assert.Equal("1234", ld.Secrets.First().Value);
        Assert.Equal(EntryKind.Login, login.Entry.Kind);

        var card = imported.First(i => i.Entry.Name == "Platinum");
        var cd = Assert.IsType<CardSecureData>(card.Payload);
        Assert.Equal("visa", card.Entry.Brand);
        Assert.Equal("4111111111111111", cd.Number);
        Assert.Equal("09", cd.ExpiryMonth);
        Assert.Equal("28", cd.ExpiryYear);
        Assert.Equal("123", cd.Cvv);
        Assert.Equal("ANA S", cd.Holder);

        var doc = imported.First(i => i.Entry.Name == "Insurance");
        var dd = Assert.IsType<EntrySecureData>(doc.Payload);
        Assert.Equal("P-100", dd.Fields.First().Value);
        Assert.Equal("Replacement code", dd.Secrets.First().Name);

        var physical = imported.First(i => i.Entry.Name == "Safe combo");
        var pd = Assert.IsType<EntrySecureData>(physical.Payload);
        Assert.Equal("7-14-21", pd.Fields.Single().Value);
    }

    [Fact]
    public void Parse_HandlesQuotedCellsWithCommasNewlinesAndQuotes()
    {
        var csv = "Name,Kind,Field,Value\r\n"
                + "\"Item, A\",Secure note,Notes,\"line one\nline \"\"two\"\"\"\r\n"
                + "\"Item, A\",Secure note,\"[secret] pass\",q\r\n";

        var imported = CsvImporter.Parse(Encoding.UTF8.GetBytes(csv));

        var item = Assert.Single(imported);
        Assert.Equal(EntryKind.Note, item.Entry.Kind);
        var data = Assert.IsType<EntrySecureData>(item.Payload);
        Assert.Equal("line one\nline \"two\"", data.Notes);
        Assert.Equal("pass", data.Secrets.Single().Name);
        Assert.Equal("q", data.Secrets.Single().Value);
    }

    [Fact]
    public void Parse_SkipsUnknownKindsAndTrimsRows()
    {
        var csv = "Name,Kind,Field,Value\r\n"
                + ",Really Weird,Name,x\r\n"
                + "Real,Secure note,Notes,hello\r\n";
        var imported = CsvImporter.Parse(Encoding.UTF8.GetBytes(csv));

        Assert.Single(imported);
    }

    [Fact]
    public void Parse_RejectsEmptyInput()
    {
        Assert.Empty(CsvImporter.Parse(Array.Empty<byte>()));
    }
}