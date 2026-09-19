using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using Microsoft.Data.Sqlite;
using CardVault.Models;
using CardVault.Security;

namespace CardVault.Data;

/// <summary>
/// SQLite vault. Both cards and non-card entries share one table; every sensitive
/// field lives inside an AES-256-GCM encrypted blob per entry, so the file on disk
/// contains no plaintext data.
/// </summary>
public sealed class VaultDatabase : IDisposable
{
    private const string Version = "1";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _path;
    private readonly VaultSession _session;
    private SqliteConnection? _conn;
    private readonly object _sync = new();

    public VaultDatabase(string path, VaultSession session)
    {
        _path = path;
        _session = session;
    }

    public void Open()
    {
        AppPaths.EnsureDataDir();
        _conn = new SqliteConnection($"Data Source={_path}");
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS meta(
                key   TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS entries(
                id         TEXT PRIMARY KEY,
                kind       TEXT NOT NULL,
                name       TEXT NOT NULL,
                brand      TEXT NOT NULL,
                accent     INTEGER NOT NULL DEFAULT -1,
                secure     BLOB NOT NULL,
                tags       TEXT NOT NULL DEFAULT '',
                folder_id  TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS folders(
                id         TEXT PRIMARY KEY,
                parent_id  TEXT NOT NULL DEFAULT '',
                name       TEXT NOT NULL,
                icon       TEXT NOT NULL DEFAULT 'folder',
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
        MigrateLegacyCards();
        EnsureColumn("entries", "tags", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn("entries", "folder_id", "TEXT NOT NULL DEFAULT ''");
    }

    private void EnsureColumn(string table, string column, string definition)
    {
        lock (_sync)
        using (var probe = _conn!.CreateCommand())
        {
            probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $name";
            probe.Parameters.AddWithValue("$name", column);
            if (Convert.ToInt64(probe.ExecuteScalar() ?? 0L) > 0) return;
        }

        Execute(c =>
        {
            c.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            c.ExecuteNonQuery();
        });
    }

    /// <summary>Migrate a pre-kinds vault file (cards table) into entries.</summary>
    private void MigrateLegacyCards()
    {
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='cards'
                """;
            if (Convert.ToInt64(cmd.ExecuteScalar() ?? 0L) == 0) return;
        }

        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO entries(id, kind, name, brand, accent, secure, created_at, updated_at)
                SELECT id, 'card', name, brand, accent, secure, created_at, updated_at FROM cards;
                DROP TABLE cards;
                """;
            cmd.ExecuteNonQuery();
        }
    }

    public bool HasMasterKey => GetMeta("version") is not null;

    // ============================= setup =============================

    public void CreateVault(string password)
    {
        var salt = CryptoService.RandomBytes(16);
        var key = CryptoService.DeriveKey(password, salt, CryptoService.KdfIterations);
        SetMeta("kdf_salt", Convert.ToBase64String(salt));
        SetMeta("kdf_iter", CryptoService.KdfIterations.ToString());
        SetMeta("master_hash", Convert.ToBase64String(CryptoService.ComputeSealHash(key)));
        SetMeta("version", Version);
        SetMeta("lock_timeout_minutes", "5");
        CryptographicOperations.ZeroMemory(salt);
        _session.Open(key);
    }

    public bool TryUnlock(string password)
    {
        var iter = int.Parse(GetMeta("kdf_iter") ?? CryptoService.KdfIterations.ToString());
        var salt = Convert.FromBase64String(GetMeta("kdf_salt") ?? string.Empty);
        var expected = Convert.FromBase64String(GetMeta("master_hash") ?? string.Empty);

        var key = CryptoService.DeriveKey(password, salt, iter);
        var actual = CryptoService.ComputeSealHash(key);

        if (!CryptoService.SlowEquals(expected, actual))
        {
            CryptographicOperations.ZeroMemory(key);
            return false;
        }

        CryptographicOperations.ZeroMemory(salt);
        _session.Open(key);
        return true;
    }

    public void ChangePassword(string newPassword)
    {
        var oldKey = _session.Key ?? throw new InvalidOperationException("Vault is locked.");
        var salt = CryptoService.RandomBytes(16);
        var newKey = CryptoService.DeriveKey(newPassword, salt, CryptoService.KdfIterations);

        var entries = ListEntries();
        foreach (var entry in entries)
        {
            var json = CryptoService.Decrypt(oldKey, entry.SecureBlob, entry.Id);
            entry.SecureBlob = CryptoService.Encrypt(newKey, json, entry.Id);
            CryptographicOperations.ZeroMemory(json);
            UpdateBlob(entry);
        }

        SetMeta("kdf_salt", Convert.ToBase64String(salt));
        SetMeta("kdf_iter", CryptoService.KdfIterations.ToString());
        SetMeta("master_hash", Convert.ToBase64String(CryptoService.ComputeSealHash(newKey)));
        CryptographicOperations.ZeroMemory(salt);
        _session.Open(newKey);
    }

    // ============================= entries =============================

    public VaultEntry CreateCard(string name, string brand, int accent, CardSecureData data, string tags = "")
    {
        var entry = NewEntry(name, EntryKind.Card, brand, accent);
        entry.Tags = tags;
        entry.SecureBlob = EncryptPayload(entry, data);
        return entry;
    }

    public VaultEntry CreateEntry(string name, EntryKind kind, int accent, EntrySecureData data, string tags = "")
    {
        var entry = NewEntry(name, kind, string.Empty, accent);
        entry.Tags = tags;
        entry.SecureBlob = EncryptPayload(entry, data);
        return entry;
    }

    public CardSecureData DecryptCard(VaultEntry entry)
    {
        var json = DecryptJson(entry);
        try
        {
            return JsonSerializer.Deserialize<CardSecureData>(json, JsonOptions)
                ?? new CardSecureData();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    public EntrySecureData DecryptEntry(VaultEntry entry)
    {
        var json = DecryptJson(entry);
        try
        {
            return JsonSerializer.Deserialize<EntrySecureData>(json, JsonOptions)
                ?? new EntrySecureData();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    public void UpdateCard(VaultEntry entry, string name, string brand, int accent, CardSecureData data, string tags = "")
    {
        entry.Kind = EntryKind.Card;
        entry.Name = name;
        entry.Brand = brand;
        entry.Accent = accent;
        entry.Tags = tags;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.SecureBlob = EncryptPayload(entry, data);
        UpdateEntryRow(entry);
    }

    public void UpdateEntryData(VaultEntry entry, string name, EntryKind kind, int accent, EntrySecureData data,
        string tags = "")
    {
        entry.Kind = kind;
        entry.Name = name;
        entry.Brand = string.Empty;
        entry.Accent = accent;
        entry.Tags = tags;
        entry.UpdatedAt = DateTimeOffset.UtcNow;
        entry.SecureBlob = EncryptPayload(entry, data);
        UpdateEntryRow(entry);
    }

    public void SaveImported(VaultEntry entry, string name, EntryKind kind, string brand, int accent,
        CardSecureData? card, EntrySecureData? generic)
    {
        entry.Name = name;
        entry.Kind = kind;
        entry.Brand = brand;
        entry.Accent = accent;
        if (card is not null)
            entry.SecureBlob = EncryptPayload(entry, card);
        else if (generic is not null)
            entry.SecureBlob = EncryptPayload(entry, generic);
        var exists = GetEntry(entry.Id) is not null;
        if (exists) UpdateEntryRow(entry); else InsertEntry(entry);
    }

    public void InsertEntry(VaultEntry entry) => Execute(c =>
    {
        c.CommandText = """
            INSERT INTO entries(id, kind, name, brand, accent, secure, tags, folder_id, created_at, updated_at)
            VALUES ($id, $kind, $name, $brand, $accent, $secure, $tags, $folder, $created, $updated)
            """;
        BindEntry(c, entry);
        c.ExecuteNonQuery();
    });

    public void UpdateEntryRow(VaultEntry entry) => Execute(c =>
    {
        c.CommandText = """
            UPDATE entries SET kind=$kind, name=$name, brand=$brand, accent=$accent, secure=$secure, tags=$tags, folder_id=$folder, updated_at=$updated
            WHERE id=$id
            """;
        BindEntry(c, entry);
        c.ExecuteNonQuery();
    });

    public void DeleteEntry(string id) => Execute(c =>
    {
        c.CommandText = "DELETE FROM entries WHERE id=$id";
        c.Parameters.AddWithValue("$id", id);
        c.ExecuteNonQuery();
    });

    public List<VaultEntry> ListEntries()
    {
        var list = new List<VaultEntry>();
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = "SELECT id, kind, name, brand, accent, secure, tags, folder_id, created_at, updated_at FROM entries ORDER BY updated_at DESC";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadEntry(reader));
            }
        }
        return list;
    }

    public List<VaultEntry> ListEntriesByFolder(string folderId)
    {
        var list = new List<VaultEntry>();
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = "SELECT id, kind, name, brand, accent, secure, tags, folder_id, created_at, updated_at FROM entries WHERE folder_id=$folder ORDER BY updated_at DESC";
            cmd.Parameters.AddWithValue("$folder", folderId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadEntry(reader));
            }
        }
        return list;
    }

    public VaultEntry? GetEntry(string id)
    {
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = "SELECT id, kind, name, brand, accent, secure, tags, folder_id, created_at, updated_at FROM entries WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadEntry(reader) : null;
        }
    }

    // ============================= folders =============================

    public Folder CreateFolder(string name, string icon, string parentId = "", string? id = null)
    {
        var folder = new Folder
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            Name = name,
            Icon = string.IsNullOrEmpty(icon) ? "folder" : icon,
            ParentId = parentId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        InsertFolder(folder);
        return folder;
    }

    public void InsertFolder(Folder folder) => Execute(c =>
    {
        c.CommandText = """
            INSERT INTO folders(id, parent_id, name, icon, created_at, updated_at)
            VALUES ($id, $parent, $name, $icon, $created, $updated)
            """;
        BindFolder(c, folder);
        c.ExecuteNonQuery();
    });

    public void UpdateFolder(Folder folder) => Execute(c =>
    {
        c.CommandText = """
            UPDATE folders SET parent_id=$parent, name=$name, icon=$icon, updated_at=$updated
            WHERE id=$id
            """;
        BindFolder(c, folder);
        c.ExecuteNonQuery();
    });

    public void MoveEntryToFolder(string entryId, string folderId) => Execute(c =>
    {
        c.CommandText = "UPDATE entries SET folder_id=$folder, updated_at=$updated WHERE id=$id";
        c.Parameters.AddWithValue("$folder", folderId);
        c.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        c.Parameters.AddWithValue("$id", entryId);
        c.ExecuteNonQuery();
    });

    public void MoveFolder(string folderId, string parentId) => Execute(c =>
    {
        c.CommandText = "UPDATE folders SET parent_id=$parent, updated_at=$updated WHERE id=$id";
        c.Parameters.AddWithValue("$parent", parentId);
        c.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O"));
        c.Parameters.AddWithValue("$id", folderId);
        c.ExecuteNonQuery();
    });

    /// <summary>Inserts or fully replaces an entry, preserving remote timestamps (used by sync).</summary>
    public void UpsertEntry(VaultEntry entry) => Execute(c =>
    {
        c.CommandText = """
            INSERT INTO entries(id, kind, name, brand, accent, secure, tags, folder_id, created_at, updated_at)
            VALUES ($id, $kind, $name, $brand, $accent, $secure, $tags, $folder, $created, $updated)
            ON CONFLICT(id) DO UPDATE SET
                kind=$kind, name=$name, brand=$brand, accent=$accent, secure=$secure,
                tags=$tags, folder_id=$folder, created_at=$created, updated_at=$updated
            """;
        BindEntry(c, entry);
        c.ExecuteNonQuery();
    });

    /// <summary>Inserts or fully replaces a folder, preserving remote timestamps (used by sync).</summary>
    public void UpsertFolder(Folder folder) => Execute(c =>
    {
        c.CommandText = """
            INSERT INTO folders(id, parent_id, name, icon, created_at, updated_at)
            VALUES ($id, $parent, $name, $icon, $created, $updated)
            ON CONFLICT(id) DO UPDATE SET
                parent_id=$parent, name=$name, icon=$icon, created_at=$created, updated_at=$updated
            """;
        BindFolder(c, folder);
        c.ExecuteNonQuery();
    });

    /// <summary>Seals a payload under the current session key in-memory (does not persist). Used by sync.</summary>
    public void AssignSecureData(VaultEntry entry, object payload)
        => entry.SecureBlob = EncryptPayload(entry, payload);

    public List<Folder> ListFolders()
    {
        var list = new List<Folder>();
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = "SELECT id, parent_id, name, icon, created_at, updated_at FROM folders ORDER BY created_at";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(ReadFolder(reader));
            }
        }
        return list;
    }

    public Folder? GetFolder(string id)
    {
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = "SELECT id, parent_id, name, icon, created_at, updated_at FROM folders WHERE id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? ReadFolder(reader) : null;
        }
    }

    /// <summary>
    /// Removes a folder and promotes its children one level up. Entries inside
    /// the folder and its descendants are re-parented into the deleted folder's
    /// parent (or top level when it had none), never lost.
    /// </summary>
    public void DeleteFolder(string id)
    {
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            var parent = GetFolder(id)?.ParentId ?? string.Empty;
            cmd.Transaction = _conn.BeginTransaction();
            try
            {
                cmd.CommandText = """
                    UPDATE folders SET parent_id=$parent WHERE parent_id=$id;
                    UPDATE entries SET folder_id=$parent WHERE folder_id=$id;
                    DELETE FROM folders WHERE id=$id;
                    """;
                cmd.Parameters.AddWithValue("$parent", parent);
                cmd.Parameters.AddWithValue("$id", id);
                cmd.ExecuteNonQuery();
                cmd.Transaction.Commit();
            }
            catch
            {
                cmd.Transaction.Rollback();
                throw;
            }
        }
    }

    // ============================= misc =============================

    public int LockTimeoutMinutes
    {
        get => int.TryParse(GetMeta("lock_timeout_minutes"), out var v) ? v : 5;
        set => SetMeta("lock_timeout_minutes", Math.Clamp(value, 1, 120).ToString());
    }

    public string? Theme
    {
        get => GetMeta(Services.ThemeService.MetaKey);
        set => SetMeta(Services.ThemeService.MetaKey, value ?? "atelier");
    }

    public string? GetMeta(string key)
    {
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = "SELECT value FROM meta WHERE key=$k";
            cmd.Parameters.AddWithValue("$k", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void SetMeta(string key, string value)
    {
        Execute(c =>
        {
            c.CommandText = """
                INSERT INTO meta(key, value) VALUES ($k, $v)
                ON CONFLICT(key) DO UPDATE SET value=$v
                """;
            c.Parameters.AddWithValue("$k", key);
            c.Parameters.AddWithValue("$v", value);
            c.ExecuteNonQuery();
        });
    }

    public void DeleteMeta(string key)
    {
        Execute(c =>
        {
            c.CommandText = "DELETE FROM meta WHERE key=$k";
            c.Parameters.AddWithValue("$k", key);
            c.ExecuteNonQuery();
        });
    }

    /// <summary>The per-vault PBKDF2 salt embedded in the encrypted backup, or null when unset.</summary>
    public byte[]? KdfSalt
    {
        get
        {
            var encoded = GetMeta("kdf_salt");
            return encoded is null ? null : Convert.FromBase64String(encoded);
        }
    }

    /// <summary>The per-vault PBKDF2 iteration count.</summary>
    public int KdfIterations => int.TryParse(GetMeta("kdf_iter"), out var v) ? v : CryptoService.KdfIterations;

    private void UpdateBlob(VaultEntry entry) => Execute(c =>
    {
        c.CommandText = "UPDATE entries SET secure=$secure, updated_at=$updated WHERE id=$id";
        c.Parameters.AddWithValue("$id", entry.Id);
        c.Parameters.AddWithValue("$secure", entry.SecureBlob);
        c.Parameters.AddWithValue("$updated", entry.UpdatedAt.ToString("O"));
        c.ExecuteNonQuery();
    });

    private static VaultEntry NewEntry(string name, EntryKind kind, string brand, int accent)
        => new()
        {
            Kind = kind,
            Name = name,
            Brand = brand,
            Accent = accent,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private byte[] EncryptPayload(VaultEntry entry, object payload)
        => CryptoService.Encrypt(
            _session.Key ?? throw new InvalidOperationException("Vault is locked."),
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions),
            entry.Id);

    private byte[] DecryptJson(VaultEntry entry)
    {
        if (entry.SecureBlob.Length == 0)
            throw new InvalidDataException("Entry has no secure payload.");

        return CryptoService.Decrypt(
            _session.Key ?? throw new InvalidOperationException("Vault is locked."),
            entry.SecureBlob,
            entry.Id);
    }

    private static void BindEntry(SqliteCommand c, VaultEntry r)
    {
        c.Parameters.AddWithValue("$id", r.Id);
        c.Parameters.AddWithValue("$kind", r.Kind.ToString().ToLowerInvariant());
        c.Parameters.AddWithValue("$name", r.Name);
        c.Parameters.AddWithValue("$brand", r.Brand);
        c.Parameters.AddWithValue("$accent", r.Accent);
        c.Parameters.AddWithValue("$secure", r.SecureBlob);
        c.Parameters.AddWithValue("$tags", r.Tags);
        c.Parameters.AddWithValue("$folder", r.FolderId);
        c.Parameters.AddWithValue("$created", r.CreatedAt.ToString("O"));
        c.Parameters.AddWithValue("$updated", r.UpdatedAt.ToString("O"));
    }

    private static VaultEntry ReadEntry(SqliteDataReader reader)
    {
        var kind = System.Enum.TryParse<EntryKind>(reader.GetString(1), true, out var k)
            ? k
            : EntryKind.Card;
        return new VaultEntry
        {
            Id = reader.GetString(0),
            Kind = kind,
            Name = reader.GetString(2),
            Brand = reader.GetString(3),
            Accent = reader.GetInt32(4),
            SecureBlob = (byte[])reader.GetValue(5),
            Tags = reader.GetString(6),
            FolderId = reader.GetString(7),
            CreatedAt = DateTimeOffset.Parse(reader.GetString(8)),
            UpdatedAt = DateTimeOffset.Parse(reader.GetString(9)),
        };
    }

    private static void BindFolder(SqliteCommand c, Folder f)
    {
        c.Parameters.AddWithValue("$id", f.Id);
        c.Parameters.AddWithValue("$parent", f.ParentId);
        c.Parameters.AddWithValue("$name", f.Name);
        c.Parameters.AddWithValue("$icon", f.Icon);
        c.Parameters.AddWithValue("$created", f.CreatedAt.ToString("O"));
        c.Parameters.AddWithValue("$updated", f.UpdatedAt.ToString("O"));
    }

    private static Folder ReadFolder(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        ParentId = reader.GetString(1),
        Name = reader.GetString(2),
        Icon = reader.GetString(3),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(4)),
        UpdatedAt = DateTimeOffset.Parse(reader.GetString(5)),
    };

    private void Execute(Action<SqliteCommand> action)
    {
        lock (_sync)
        using (var cmd = _conn!.CreateCommand())
        {
            action(cmd);
        }
    }

    public void Dispose() => Interlocked.Exchange(ref _conn, null)?.Dispose();
}