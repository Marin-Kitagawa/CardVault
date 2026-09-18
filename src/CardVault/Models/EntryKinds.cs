using System;
using System.Collections.Generic;
using System.Linq;

namespace CardVault.Models;

/// <summary>
/// Static metadata for every entry kind: display names, picker blurbs, glyphs and
/// the labelled field templates that drive both the generic editor and the
/// reveal-gated detail view. Glyphs are Lucide-style stroke paths in a 24-unit
/// view-box (see Assets/Icons/LICENSES.Lucide.txt), rendered via StreamGeometry.
/// </summary>
public static class EntryKinds
{
    public static readonly string GraphiteStart = "#38342D";
    public static readonly string GraphiteEnd = "#1A1813";

    public static EntryKindInfo For(EntryKind kind) => All.First(k => k.Kind == kind);

    public static bool IsCard(EntryKind kind) => kind == EntryKind.Card;

    public static string DisplayName(EntryKind kind) => For(kind).DisplayName;

    public static IReadOnlyList<EntryKindInfo> All { get; } = new List<EntryKindInfo>
    {
        new(EntryKind.Card, "Card", "Payment card — number, holder, expiry, CVV.", IconPaths.CreditCard, Array.Empty<EntryFieldDef>()),
        new(EntryKind.Login, "Login", "A website or app sign-in — url, username, password.", IconPaths.Key, new[]
        {
            new EntryFieldDef("Website or app", false, "e.g. example.com"),
            new EntryFieldDef("Username", false, "Username or email"),
            new EntryFieldDef("Password", true, "Consider using a long passphrase"),
        }),
        new(EntryKind.Financial, "Account", "Bank, brokerage or loan account details.", IconPaths.Account, new[]
        {
            new EntryFieldDef("Institution", false, "e.g. Bank Name"),
            new EntryFieldDef("Account type", false, "Checking, savings, investment, loan…"),
            new EntryFieldDef("Account name", false, "Label you use for this account"),
            new EntryFieldDef("Account number", true, "Account / IBAN number"),
            new EntryFieldDef("Routing / SWIFT / BIC", true, "Routing or SWIFT code"),
        }),
        new(EntryKind.Crypto, "Crypto", "Wallet address, seed phrase or private key.", IconPaths.Crypto, new[]
        {
            new EntryFieldDef("Network", false, "e.g. Bitcoin, Ethereum"),
            new EntryFieldDef("Wallet address", false, "Public address"),
            new EntryFieldDef("Seed phrase", true, "12 or 24 words"),
            new EntryFieldDef("Private key", true, "Private / extended key"),
        }),
        new(EntryKind.Identity, "Identity", "Passport, national ID, licence, SSN document.", IconPaths.Identity, new[]
        {
            new EntryFieldDef("Document type", false, "Passport, National ID, Licence, SSN…"),
            new EntryFieldDef("Full name", false, "Name on the document"),
            new EntryFieldDef("Document number", true, "Number printed on the document"),
            new EntryFieldDef("Issuing country", false, "Country that issued it"),
            new EntryFieldDef("Date of birth", false, "yyyy-mm-dd"),
            new EntryFieldDef("Expiry date", false, "yyyy-mm-dd"),
        }),
        new(EntryKind.Document, "Document", "Key–value fields: insurance, medical, recovery keys…", IconPaths.Document, Array.Empty<EntryFieldDef>()),
        new(EntryKind.Membership, "Membership", "Reward, loyalty and club memberships.", IconPaths.Membership, new[]
        {
            new EntryFieldDef("Program", false, "e.g. Frequent Flyer"),
            new EntryFieldDef("Membership number", true, "Member / reward number"),
            new EntryFieldDef("Tier", false, "e.g. Gold"),
            new EntryFieldDef("Expires", false, "yyyy-mm-dd"),
        }),
        new(EntryKind.Gift, "Gift card", "Prepaid and store gift cards.", IconPaths.Gift, new[]
        {
            new EntryFieldDef("Store", false, "Where the card is valid"),
            new EntryFieldDef("Code", true, "Code / PIN under the scratch panel"),
            new EntryFieldDef("Balance", false, "Remaining balance"),
            new EntryFieldDef("Expires", false, "yyyy-mm-dd"),
        }),
        new(EntryKind.Physical, "Physical", "Combinations: safe, deposit box, locker, gate.", IconPaths.Physical, new[]
        {
            new EntryFieldDef("Item / location", false, "e.g. Home safe"),
            new EntryFieldDef("Type", false, "Safe, deposit box, locker, alarm, gate…"),
            new EntryFieldDef("Combination", true, "Code / combination"),
        }),
        new(EntryKind.Note, "Secure note", "A private free-text note.", IconPaths.Note, Array.Empty<EntryFieldDef>()),
    };
}

/// <summary>
/// Lucide-style glyphs as combined stroke path data. Originals are ISC-licensed
/// Lucide icons (see Assets/Icons/LICENSES.Lucide.txt); circles are emitted as
/// twin arcs so every glyph renders with a single stroked Path.
/// </summary>
public static class IconPaths
{
    public const string CreditCard = "M4 5h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z M2 10h20 M6 14h2";
    public const string Key = "M2 21l9.6-9.6 M7.5 15.5l2.3 2.3a1 1 0 0 1 0 1.4l-2.1 2.1a1 1 0 0 1-1.4 0L4 19 M15.5 2a5.5 5.5 0 1 1 0 11 5.5 5.5 0 1 1 0-11";
    public const string Account = "M10 18v-7 M11.119 2.205a2 2 0 0 1 1.762 0l7.84 3.846A.5.5 0 0 1 20.5 7h-17a.5.5 0 0 1-.22-.949z M14 18v-7 M18 18v-7 M3 22h18 M6 18v-7";
    public const string Crypto = "M11.767 19.089c4.924.868 6.14-6.025 1.216-6.894m-1.216 6.894L5.86 18.047m5.908 1.042-.347 1.97m1.563-8.864c4.924.869 6.14-6.025 1.215-6.893m-1.215 6.893-3.94-.694m5.155-6.2L8.29 4.26m5.908 1.042.348-1.97M7.48 20.364l3.126-17.727";
    public const string Identity = "M16 2v2 M7 21v-2a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v2 M8 2v2 M12 7a3 3 0 1 0 0 6 3 3 0 0 0 0-6 M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z";
    public const string Document = "M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z M14 2v5a1 1 0 0 0 1 1h5 M10 9H8 M16 13H8 M16 17H8";
    public const string Note = "M21 9a2.4 2.4 0 0 0-.706-1.706l-3.588-3.588A2.4 2.4 0 0 0 15 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2z M15 3v5a1 1 0 0 0 1 1h5";
    public const string Membership = "m15.477 12.89 1.515 8.526a.5.5 0 0 1-.81.47l-3.58-2.687a1 1 0 0 0-1.197 0l-3.586 2.686a.5.5 0 0 1-.81-.469l1.514-8.526 M12 2a6 6 0 1 1 0 12 6 6 0 1 1 0-12";
    public const string Gift = "M12 7v14 M20 11v8a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2v-8 M7.5 7a1 1 0 0 1 0-5A4.8 8 0 0 1 12 7a4.8 8 0 0 1 4.5-5 1 1 0 0 1 0 5 M4 7h16a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V8a1 1 0 0 1 1-1z";
    public const string Physical = "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z";
    public const string Plus = "M5 12h14 M12 5v14";
    public const string Lock = "M18 8h1a4 4 0 0 1 4 4v8a4 4 0 0 1-4 4H5a4 4 0 0 1-4-4v-8a4 4 0 0 1 4-4h1 M6 8V7a6 6 0 0 1 12 0v1 M12 11v5";
    public const string Wifi = "M12 20h.01 M2 8.82a15 15 0 0 1 20 0 M5 12.859a10 10 0 0 1 14 0 M8.5 16.429a5 5 0 0 1 7 0";
}