using CardVault.Data;
using CardVault.Models;
using CardVault.Security;
using CardVault.ViewModels;

namespace CardVault.Tests;

public class FolderTests
{
    private static (VaultDatabase db, VaultSession session) CreateVault(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cardvault-folder-{name}-{Guid.NewGuid():N}.db");
        var session = new VaultSession();
        var db = new VaultDatabase(path, session);
        db.Open();
        session.Open(new byte[32]);
        return (db, session);
    }

    [Fact]
    public void CreateFolder_ListFolders_RoundTrips()
    {
        var (db, session) = CreateVault("list");
        try
        {
            var root = db.CreateFolder("Banking", "wallet");
            var nested = db.CreateFolder("Visa", "credit-card", root.Id);

            var all = db.ListFolders();
            Assert.Equal(2, all.Count);

            var loaded = db.GetFolder(nested.Id)!;
            Assert.NotNull(loaded);
            Assert.Equal("Visa", loaded.Name);
            Assert.Equal("credit-card", loaded.Icon);
            Assert.Equal(root.Id, loaded.ParentId);
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void CreateFolder_DefaultIcon_IsFolder()
    {
        var (db, session) = CreateVault("icon");
        try
        {
            var folder = db.CreateFolder("Plain", string.Empty);
            Assert.Equal("folder", folder.Icon);
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void UpdateFolder_Renames_Reparents_ChangesIcon()
    {
        var (db, session) = CreateVault("update");
        try
        {
            var a = db.CreateFolder("A", "folder");
            var b = db.CreateFolder("B", "folder");
            db.UpdateFolder(new Folder { Id = b.Id, ParentId = a.Id, Name = "Bee", Icon = "heart" });

            var loaded = db.GetFolder(b.Id)!;
            Assert.Equal("Bee", loaded.Name);
            Assert.Equal("heart", loaded.Icon);
            Assert.Equal(a.Id, loaded.ParentId);
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void MoveEntryToFolder_And_ListEntriesByFolder()
    {
        var (db, session) = CreateVault("entries");
        try
        {
            var folder = db.CreateFolder("Important", "key");
            var entry = db.CreateNote("Reference", "NTG-1234");
            db.InsertEntry(entry);
            db.MoveEntryToFolder(entry.Id, folder.Id);

            var moved = db.GetEntry(entry.Id)!;
            Assert.Equal(folder.Id, moved.FolderId);
            Assert.Single(db.ListEntriesByFolder(folder.Id));

            db.MoveEntryToFolder(entry.Id, string.Empty);
            Assert.Empty(db.ListEntriesByFolder(folder.Id));
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void DeleteFolder_PromotesChildrenAndEntries()
    {
        var (db, session) = CreateVault("delete");
        try
        {
            var parent = db.CreateFolder("Parent", "folder");
            var child = db.CreateFolder("Child", "star", parent.Id);
            var grandchild = db.CreateFolder("Grandchild", "heart", child.Id);

            var topEntry = db.CreateNote("Top entry", "1");
            db.InsertEntry(topEntry);
            db.MoveEntryToFolder(topEntry.Id, child.Id);
            var deepEntry = db.CreateNote("Deep entry", "2");
            db.InsertEntry(deepEntry);
            db.MoveEntryToFolder(deepEntry.Id, grandchild.Id);

            db.DeleteFolder(child.Id);

            Assert.Null(db.GetFolder(child.Id));

            var promotedGrandchild = db.GetFolder(grandchild.Id)!;
            Assert.NotNull(promotedGrandchild);
            Assert.Equal(parent.Id, promotedGrandchild.ParentId);

            var promotedChild = db.ListFolders().First(f => f.Id == grandchild.Id);
            Assert.Equal(parent.Id, promotedChild.ParentId);
            Assert.Equal(parent.Id, db.GetEntry(topEntry.Id)!.FolderId);
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void DeleteFolder_WithNoParent_MovesEntriesToTopLevel()
    {
        var (db, session) = CreateVault("top");
        try
        {
            var folder = db.CreateFolder("Loose", "folder");
            var entry = db.CreateNote("Note", "x");
            db.InsertEntry(entry);
            db.MoveEntryToFolder(entry.Id, folder.Id);

            db.DeleteFolder(folder.Id);

            Assert.Null(db.GetFolder(folder.Id));
            Assert.Equal(string.Empty, db.GetEntry(entry.Id)!.FolderId);
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void MoveFolder_Reparents()
    {
        var (db, session) = CreateVault("move");
        try
        {
            var a = db.CreateFolder("A", "folder");
            var b = db.CreateFolder("B", "folder");
            db.MoveFolder(b.Id, a.Id);

            var loaded = db.GetFolder(b.Id)!;
            Assert.Equal(a.Id, loaded.ParentId);
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void CanAssign_AllowsValidParent()
    {
        var (db, session) = CreateVault("guard");
        try
        {
            var root = db.CreateFolder("Root", "folder");
            var child = db.CreateFolder("Child", "folder");
            var sibling = db.CreateFolder("Sibling", "folder");

            var folders = new List<Folder> { root, child, sibling };
            Assert.True(FolderManagerViewModel.CanAssign(folders, child.Id, root.Id));
            Assert.True(FolderManagerViewModel.CanAssign(folders, child.Id, sibling.Id));
            Assert.True(FolderManagerViewModel.CanAssign(folders, root.Id, string.Empty));
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void CanAssign_RejectsCyclesButAllowsValidMoves()
    {
        var (db, session) = CreateVault("cycle");
        try
        {
            var root = db.CreateFolder("Root", "folder");
            var child = db.CreateFolder("Child", "folder", root.Id);
            var grandchild = db.CreateFolder("Grandchild", "folder", child.Id);
            var sibling = db.CreateFolder("Sibling", "folder");

            var folders = new List<Folder> { root, child, grandchild, sibling };

            // Self-parenting is never allowed.
            Assert.False(FolderManagerViewModel.CanAssign(folders, child.Id, child.Id));
            // A folder cannot move into its own subtree (would form a cycle).
            Assert.False(FolderManagerViewModel.CanAssign(folders, root.Id, child.Id));
            Assert.False(FolderManagerViewModel.CanAssign(folders, root.Id, grandchild.Id));
            Assert.False(FolderManagerViewModel.CanAssign(folders, child.Id, grandchild.Id));
            // Valid moves are allowed.
            Assert.True(FolderManagerViewModel.CanAssign(folders, child.Id, sibling.Id));
            Assert.True(FolderManagerViewModel.CanAssign(folders, sibling.Id, child.Id));
            Assert.True(FolderManagerViewModel.CanAssign(folders, grandchild.Id, child.Id));
            Assert.True(FolderManagerViewModel.CanAssign(folders, root.Id, string.Empty));
        }
        finally { session.Lock(); db.Dispose(); }
    }
}

internal static class VaultTestExtensions
{
    public static VaultEntry CreateNote(this VaultDatabase db, string name, string note)
    {
        var entry = db.CreateEntry(name, EntryKind.Note, -1,
            new EntrySecureData { Notes = note, Fields = new(), Secrets = new() });
        return entry;
    }
}