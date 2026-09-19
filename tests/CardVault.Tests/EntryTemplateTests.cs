using System.Linq;
using CardVault.Models;
using Xunit;

namespace CardVault.Tests;

public class EntryTemplateTests
{
    [Fact]
    public void ForKind_OffersTemplatesForIdentityAndDocumentOnly()
    {
        Assert.NotEmpty(EntryTemplates.ForKind(EntryKind.Identity));
        Assert.NotEmpty(EntryTemplates.ForKind(EntryKind.Document));
        Assert.Empty(EntryTemplates.ForKind(EntryKind.Card));
        Assert.Empty(EntryTemplates.ForKind(EntryKind.Note));
        Assert.Empty(EntryTemplates.ForKind(EntryKind.Login));
    }

    [Fact]
    public void All_HaveUniqueIdsAndNonEmptyFieldLabels()
    {
        var ids = EntryTemplates.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        foreach (var template in EntryTemplates.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(template.Name));
            Assert.NotEmpty(template.Fields);
            foreach (var field in template.Fields)
                Assert.False(string.IsNullOrWhiteSpace(field.Label));
        }
    }

    [Fact]
    public void SensitiveFields_AreMarkedSecret()
    {
        var passport = EntryTemplates.ById("passport");
        Assert.NotNull(passport);
        Assert.Contains(passport!.Fields, f => f.Label == "Passport number" && f.Secret);
        Assert.Contains(passport.Fields, f => f.Label == "Full name" && !f.Secret);
    }

    [Fact]
    public void ById_FindsExistingAndReturnsNullForUnknown()
    {
        Assert.Null(EntryTemplates.ById("nope"));
        Assert.Null(EntryTemplates.ById(null));
        Assert.Equal("tax-id", EntryTemplates.ById("tax-id")!.Id);
        Assert.Null(EntryTemplates.ById("TAX-ID")); // case-sensitive ids
    }
}