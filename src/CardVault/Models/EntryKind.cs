using System.Collections.Generic;

namespace CardVault.Models;

/// <summary>
/// The kind of information an entry holds. Cards keep the dedicated card face,
/// preview formatting and brand detection; every other kind is a labelled set of
/// fields plus optional free-form rows, all stored inside the encrypted payload.
/// </summary>
public enum EntryKind
{
    Card,
    Login,
    Financial,
    Crypto,
    Identity,
    Document,
    Membership,
    Gift,
    Physical,
    Note,
}

/// <summary>One fixed, labelled field in an entry template.</summary>
public sealed record EntryFieldDef(string Label, bool Masked, string Placeholder);

public sealed record EntryKindInfo(
    EntryKind Kind,
    string DisplayName,
    string Blurb,
    string IconPath,
    IReadOnlyList<EntryFieldDef> Fields);