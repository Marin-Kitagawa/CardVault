using System.Collections.Generic;
using System.Linq;

namespace CardVault.Models;

/// <summary>A starter layout for entry kinds that keep key–value fields.</summary>
public sealed record EntryTemplate(string Id, string Name, IReadOnlyList<TemplateFieldDef> Fields);

/// <summary>
/// One template field. <see cref="Secret"/> marks values that should be masked in
/// the detail view (like a password field) — passport numbers, member IDs, and so on.
/// </summary>
public sealed record TemplateFieldDef(string Label, bool Secret = false);

public static class EntryTemplates
{
    public static IReadOnlyList<EntryTemplate> All { get; } = new List<EntryTemplate>
    {
        new("passport", "Passport", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("Passport number", Secret: true),
            new TemplateFieldDef("Nationality"),
            new TemplateFieldDef("Issuing country"),
            new TemplateFieldDef("Date of birth"),
            new TemplateFieldDef("Date of expiry"),
        }),
        new("national-id", "National ID", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("ID number", Secret: true),
            new TemplateFieldDef("Issuing country"),
            new TemplateFieldDef("Issuing authority"),
            new TemplateFieldDef("Date of birth"),
            new TemplateFieldDef("Date of expiry"),
        }),
        new("driving-licence", "Driving licence", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("Licence number", Secret: true),
            new TemplateFieldDef("Issuing country"),
            new TemplateFieldDef("Class"),
            new TemplateFieldDef("Date of birth"),
            new TemplateFieldDef("Date of expiry"),
        }),
        new("ssn", "Social Security", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("Social Security Number", Secret: true),
            new TemplateFieldDef("Issuing country"),
        }),
        new("health-insurance", "Health insurance", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("Member ID", Secret: true),
            new TemplateFieldDef("Policy holder"),
            new TemplateFieldDef("Plan name"),
            new TemplateFieldDef("Group number", Secret: true),
            new TemplateFieldDef("Date of birth"),
        }),
        new("residence-permit", "Residence permit", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("Permit number", Secret: true),
            new TemplateFieldDef("Issuing country"),
            new TemplateFieldDef("Status"),
            new TemplateFieldDef("Date of birth"),
            new TemplateFieldDef("Date of expiry"),
        }),
        new("voter-id", "Voter ID", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("Voter ID number", Secret: true),
            new TemplateFieldDef("State / Region"),
            new TemplateFieldDef("Date of birth"),
        }),
        new("tax-id", "Tax ID", new[]
        {
            new TemplateFieldDef("Full name"),
            new TemplateFieldDef("Tax ID number", Secret: true),
            new TemplateFieldDef("Issuing country"),
            new TemplateFieldDef("Address"),
        }),
    };

    /// <summary>Templates offered for an entry kind; empty for card and untyped kinds.</summary>
    public static IReadOnlyList<EntryTemplate> ForKind(EntryKind kind) =>
        kind is EntryKind.Identity or EntryKind.Document ? All : System.Array.Empty<EntryTemplate>();

    public static EntryTemplate? ById(string? id) =>
        string.IsNullOrEmpty(id) ? null : All.FirstOrDefault(t => t.Id == id);
}