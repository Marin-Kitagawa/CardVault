using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CardVault.Data;
using CardVault.Models;
using CardVault.Security;
using CardVault.Services;
using Xunit;

namespace CardVault.Tests;

public class SyncServiceTests
{
    private const string Pw = "correct-horse-battery-staple";

    private static (VaultDatabase db, VaultSession session) CreateVault(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"cardvault-sync-{name}-{Guid.NewGuid():N}.db");
        var session = new VaultSession();
        var db = new VaultDatabase(path, session);
        db.Open();
        session.Open(new byte[32]);
        return (db, session);
    }

    private static string TempFolder(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cv-sync-{name}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string SyncFilePath(string folder) => Path.Combine(folder, SyncService.FileName);

    private static byte[] Sha256Bytes(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return sha.ComputeHash(fs);
    }

    // ============================= preferences & salt adoption =============================

    [Fact]
    public void SavePrefsAndReadPrefs_RoundTrips()
    {
        var (db, session) = CreateVault("prefs");
        var sync = new SyncService(db, session);
        try
        {
            sync.SavePrefs(new SyncPrefs(@"C:\Shared Vault", true, null));
            var prefs = sync.ReadPrefs();
            Assert.True(prefs.Enabled);
            Assert.Equal(@"C:\Shared Vault", prefs.Folder);

            sync.SavePrefs(new SyncPrefs(string.Empty, true, null));
            var cleared = sync.ReadPrefs();
            Assert.False(cleared.Enabled);
            Assert.Equal(string.Empty, cleared.Folder);
        }
        finally { session.Lock(); db.Dispose(); }
    }

    [Fact]
    public void OnUnlocked_InstallsRandomSalt_Once()
    {
        var (db, session) = CreateVault("salt");
        var sync = new SyncService(db, session);
        try
        {
            sync.OnUnlocked(Pw);
            var first = db.GetMeta(SyncService.SaltKey);
            Assert.NotNull(first);

            sync.OnUnlocked(Pw);
            Assert.Equal(first, db.GetMeta(SyncService.SaltKey));
        }
        finally { session.Lock(); sync.Stop(); db.Dispose(); }
    }

    [Fact]
    public void AdoptRemoteSalt_CopiesHeaderSalt_AndReturnsFalseWithoutFile()
    {
        var dir = TempFolder("adopt");
        var (a, sa) = CreateVault("adopt-a");
        var (b, sb) = CreateVault("adopt-b");
        var syncB = new SyncService(b, sb);
        try
        {
            Assert.False(syncB.AdoptRemoteSalt(dir));

            var syncA = new SyncService(a, sa);
            syncA.OnUnlocked(Pw);
            syncA.SavePrefs(new SyncPrefs(dir, true, null));
            syncA.RunSyncAsync().GetAwaiter().GetResult();

            Assert.True(syncB.AdoptRemoteSalt(dir));
            Assert.Equal(a.GetMeta(SyncService.SaltKey), b.GetMeta(SyncService.SaltKey));
        }
        finally
        {
            sa.Lock(); sb.Lock(); a.Dispose(); b.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    // ============================= pure LWW merge =============================

    private static VaultEntry Entry(string id, DateTimeOffset updated) => new()
    {
        Id = id,
        Kind = EntryKind.Note,
        Name = "X",
        UpdatedAt = updated,
        CreatedAt = updated.AddDays(-1),
        SecureBlob = new byte[] { 0x01 },
    };

    private static SyncEntry BaseEntry(string id, DateTimeOffset updated, string notes) => new()
    {
        Id = id,
        Kind = "note",
        Name = "X",
        UpdatedAt = updated,
        CreatedAt = updated.AddDays(-1),
        Generic = new EntrySecureData { Notes = notes, Fields = new(), Secrets = new() },
    };

    [Fact]
    public void Merge_RemoteNewerWins()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var local = new LocalItem(Entry("e1", now.AddHours(-2)), new EntrySecureData { Notes = "local" });
        var baseEntry = BaseEntry("e1", now.AddHours(-1), "remote");

        var res = SyncService.Merge(new[] { local }, new Folder[0], new SyncTombstone[0],
            new[] { baseEntry }, new SyncFolder[0], new SyncTombstone[0], now.AddDays(-1), now);

        var winner = Assert.Single(res.Entries);
        Assert.Equal(now.AddHours(-1), winner.Entry.UpdatedAt);
        Assert.Equal("remote", ((EntrySecureData)winner.Payload!).Notes);
        Assert.True(res.Changed);
    }

    [Fact]
    public void Merge_LocalNewerWins()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var local = new LocalItem(Entry("e1", now.AddHours(-1)), new EntrySecureData { Notes = "local" });
        var baseEntry = BaseEntry("e1", now.AddHours(-2), "remote");

        var res = SyncService.Merge(new[] { local }, new Folder[0], new SyncTombstone[0],
            new[] { baseEntry }, new SyncFolder[0], new SyncTombstone[0], now.AddDays(-1), now);

        var winner = Assert.Single(res.Entries);
        Assert.Equal("local", ((EntrySecureData)winner.Payload!).Notes);
    }

    [Fact]
    public void Merge_TiePrefersLocalAndIsNotAChange()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var local = new LocalItem(Entry("e1", now), new EntrySecureData { Notes = "local" });
        var baseEntry = BaseEntry("e1", now, "remote");

        var res = SyncService.Merge(new[] { local }, new Folder[0], new SyncTombstone[0],
            new[] { baseEntry }, new SyncFolder[0], new SyncTombstone[0], now, now);

        Assert.Equal("local", ((EntrySecureData)Assert.Single(res.Entries).Payload!).Notes);
        Assert.False(res.Changed);
    }

    [Fact]
    public void Merge_FreshDevice_AdoptsEverything()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var baseEntry = BaseEntry("e1", now.AddDays(-5), "remote");

        var res = SyncService.Merge(new LocalItem[0], new Folder[0], new SyncTombstone[0],
            new[] { baseEntry }, new SyncFolder[0], new SyncTombstone[0], null, now);

        var adopted = Assert.Single(res.Entries);
        Assert.Equal("remote", ((EntrySecureData)adopted.Payload!).Notes);
        Assert.True(res.Changed);
    }

    [Fact]
    public void Merge_KnownEntryMissingLocally_BecomesTombstone()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var known = BaseEntry("e1", now.AddDays(-3), "gone");
        var syncLast = now.AddDays(-1);

        var res = SyncService.Merge(new LocalItem[0], new Folder[0], new SyncTombstone[0],
            new[] { known }, new SyncFolder[0], new SyncTombstone[0], syncLast, now);

        Assert.Empty(res.Entries);
        var tomb = Assert.Single(res.Tombstones);
        Assert.Equal("e1", tomb.Id);
        Assert.Equal(TombstoneKind.Entry, tomb.Kind);
        Assert.True(res.Changed);
    }

    [Fact]
    public void Merge_NewerRemoteEdit_BeatsOlderTombstone_Resurrects()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var baseEntry = BaseEntry("e1", now, "edited again");
        var tomb = new SyncTombstone { Id = "e1", Kind = TombstoneKind.Entry, DeletedAt = now.AddDays(-1) };

        var res = SyncService.Merge(new LocalItem[0], new Folder[0], new[] { tomb },
            new[] { baseEntry }, new SyncFolder[0], new SyncTombstone[0], now.AddDays(-7), now);

        Assert.Single(res.Entries);
        Assert.DoesNotContain(res.Tombstones, t => t.Id == "e1");
    }

    [Fact]
    public void Merge_NewerTombstone_DeletesLocalCopy()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var local = new LocalItem(Entry("e1", now.AddDays(-2)), new EntrySecureData());
        var tomb = new SyncTombstone { Id = "e1", Kind = TombstoneKind.Entry, DeletedAt = now };

        var res = SyncService.Merge(new[] { local }, new Folder[0], new[] { tomb },
            new SyncEntry[0], new SyncFolder[0], new SyncTombstone[0], now.AddDays(-7), now);

        Assert.Contains("e1", res.EntryIdsToDelete);
        Assert.Empty(res.Entries);
        Assert.Contains(res.Tombstones, t => t.Id == "e1");
    }

    [Fact]
    public void Merge_FolderTombstone_DeletesLocalFolder()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var folder = new Folder { Id = "f1", Name = "Banking", UpdatedAt = now.AddDays(-2) };
        var tomb = new SyncTombstone { Id = "f1", Kind = TombstoneKind.Folder, DeletedAt = now };

        var res = SyncService.Merge(new LocalItem[0], new[] { folder }, new[] { tomb },
            new SyncEntry[0], new SyncFolder[0], new SyncTombstone[0], now.AddDays(-7), now);

        Assert.Contains("f1", res.FolderIdsToDelete);
        Assert.Empty(res.Folders);
    }

    [Fact]
    public void Merge_LocalOnlyItemsAreKeptAndPushed()
    {
        var now = new DateTimeOffset(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
        var local = new LocalItem(Entry("e1", now), new EntrySecureData { Notes = "mine" });

        var res = SyncService.Merge(new[] { local }, new Folder[0], new SyncTombstone[0],
            new SyncEntry[0], new SyncFolder[0], new SyncTombstone[0], now.AddDays(-1), now);

        var kept = Assert.Single(res.Entries);
        Assert.Equal("mine", ((EntrySecureData)kept.Payload!).Notes);
        Assert.True(res.Changed);
    }

    // ============================= end-to-end file sync =============================

    [Fact]
    public async Task RunSync_WritesSnapshot_AndSecondDeviceAdopts()
    {
        var dir = TempFolder("e2e");
        var (a, sa) = CreateVault("e2e-a");
        var (b, sb) = CreateVault("e2e-b");
        try
        {
            var folder = a.CreateFolder("Banking", "wallet");
            var note = a.CreateNote("Shared note", "hello world");
            a.InsertEntry(note);
            var card = a.CreateCard("Visa", "VISA", 0, new CardSecureData { Number = "4111111111111111", Holder = "Ari" });
            a.InsertEntry(card);

            var syncA = new SyncService(a, sa);
            syncA.OnUnlocked(Pw);
            syncA.SavePrefs(new SyncPrefs(dir, true, null));

            Assert.True(await syncA.RunSyncAsync());
            Assert.True(File.Exists(SyncFilePath(dir)));

            // Second, idle device adopts the header salt and pulls everything.
            var syncB = new SyncService(b, sb);
            Assert.True(syncB.AdoptRemoteSalt(dir));
            syncB.OnUnlocked(Pw);
            syncB.SavePrefs(new SyncPrefs(dir, true, null));

            Assert.True(await syncB.RunSyncAsync());

            Assert.Equal(2, b.ListEntries().Count);
            var loadedFolder = Assert.Single(b.ListFolders());
            Assert.Equal("Banking", loadedFolder.Name);
            Assert.Equal("wallet", loadedFolder.Icon);

            var loadedNote = b.GetEntry(note.Id)!;
            Assert.Equal("Shared note", loadedNote.Name);
            Assert.Equal("hello world", b.DecryptEntry(loadedNote).Notes);

            var loadedCard = b.GetEntry(card.Id)!;
            Assert.Equal("4111111111111111", b.DecryptCard(loadedCard).Number);

            // A second sync on A changes nothing.
            Assert.False(await syncA.RunSyncAsync());
        }
        finally
        {
            sa.Lock(); sb.Lock(); a.Dispose(); b.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunSync_Deletion_PropagatesAsTombstone()
    {
        var dir = TempFolder("delete");
        var (a, sa) = CreateVault("del-a");
        var (b, sb) = CreateVault("del-b");
        try
        {
            var note = a.CreateNote("Transient", "boom");
            a.InsertEntry(note);

            var syncA = new SyncService(a, sa);
            syncA.OnUnlocked(Pw);
            syncA.SavePrefs(new SyncPrefs(dir, true, null));
            await syncA.RunSyncAsync();

            var syncB = new SyncService(b, sb);
            syncB.AdoptRemoteSalt(dir);
            syncB.OnUnlocked(Pw);
            syncB.SavePrefs(new SyncPrefs(dir, true, null));
            await syncB.RunSyncAsync();
            Assert.NotNull(b.GetEntry(note.Id));

            // A deletes the entry and pushes.
            a.DeleteEntry(note.Id);
            Assert.True(await syncA.RunSyncAsync());

            // B pulls the tombstone and removes its copy.
            Assert.True(await syncB.RunSyncAsync());
            Assert.Null(b.GetEntry(note.Id));
            Assert.Empty(b.ListEntries());
            Assert.Contains(note.Id, b.GetMeta(SyncService.TombstonesKey) ?? string.Empty);

            Assert.Null(a.GetEntry(note.Id));
        }
        finally
        {
            sa.Lock(); sb.Lock(); a.Dispose(); b.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task RunSync_WrongMasterPassword_FailsWithoutOverwriting()
    {
        var dir = TempFolder("wrongpw");
        var (a, sa) = CreateVault("wrong-a");
        var (b, sb) = CreateVault("wrong-b");
        try
        {
            var note = a.CreateNote("Secret", "only on A");
            a.InsertEntry(note);

            var syncA = new SyncService(a, sa);
            syncA.OnUnlocked(Pw);
            syncA.SavePrefs(new SyncPrefs(dir, true, null));
            await syncA.RunSyncAsync();
            var before = Sha256Bytes(SyncFilePath(dir));

            var syncB = new SyncService(b, sb);
            syncB.AdoptRemoteSalt(dir);
            syncB.OnUnlocked("a-different-password");
            syncB.SavePrefs(new SyncPrefs(dir, true, null));

            await Assert.ThrowsAsync<SyncException>(() => syncB.RunSyncAsync());
            Assert.Equal(before, Sha256Bytes(SyncFilePath(dir)));
            Assert.Empty(b.ListEntries());
        }
        finally
        {
            sa.Lock(); sb.Lock(); a.Dispose(); b.Dispose();
            Directory.Delete(dir, recursive: true);
        }
    }
}