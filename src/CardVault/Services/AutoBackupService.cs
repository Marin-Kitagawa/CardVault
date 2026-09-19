using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CardVault.Data;
using CardVault.Models;
using CardVault.Security;

namespace CardVault.Services;

/// <summary>
/// Scheduled encrypted backups. Unlike the interactive export (which prompts for a
/// passphrase), scheduled backups reuse the vault's own master key via
/// <see cref="ExportService.ExportWithKey"/>, so the resulting file can be restored
/// on any CardVault installation with your normal master password. Retention keeps
/// the most recent N files and deletes the rest.
/// </summary>
public sealed class AutoBackupService
{
    public const string FolderKey = "backup_folder";
    public const string CadenceKey = "backup_cadence";
    public const string RetainKey = "backup_retain";
    public const string LastKey = "backup_last";
    public const int DefaultRetain = 14;

    public const string Daily = "daily";
    public const string Weekly = "weekly";
    public const string Monthly = "monthly";

    private readonly VaultDatabase _db;
    private readonly VaultSession _session;
    private readonly ExportService _export;
    private readonly DispatcherTimer _timer;
    private int _running;

    public AutoBackupService(VaultDatabase db, VaultSession session, ExportService export)
    {
        _db = db;
        _session = session;
        _export = export;
        _timer = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background, async (_, _) => await OnTickAsync());
    }

    public void Start() => _timer.Start();

    public string LastError { get; private set; } = string.Empty;
    public DateTimeOffset? LastModified { get; private set; }

    public BackupPrefs ReadPrefs() => new(
        _db.GetMeta(FolderKey) ?? string.Empty,
        _db.GetMeta(CadenceKey) ?? string.Empty,
        int.TryParse(_db.GetMeta(RetainKey), out var retain) ? retain : DefaultRetain,
        ParseTimestamp(_db.GetMeta(LastKey)));

    public void SavePrefs(BackupPrefs prefs)
    {
        if (string.IsNullOrWhiteSpace(prefs.Folder))
        {
            _db.DeleteMeta(FolderKey);
            _db.DeleteMeta(CadenceKey);
            _db.DeleteMeta(RetainKey);
            return;
        }

        _db.SetMeta(FolderKey, prefs.Folder.Trim());
        _db.SetMeta(CadenceKey, prefs.Cadence);
        _db.SetMeta(RetainKey, prefs.Retain.ToString());
    }

    /// <summary>True when the configured schedule says a backup should run.</summary>
    public static bool IsDue(DateTimeOffset? lastBackup, string cadence, DateTimeOffset now)
    {
        if (cadence is not Daily and not Weekly and not Monthly) return false;
        if (lastBackup is null) return true;
        return cadence switch
        {
            Daily => lastBackup.Value.Date < now.Date,
            Weekly => lastBackup.Value.AddDays(7) <= now,
            Monthly => lastBackup.Value.AddDays(28) <= now,
            _ => false,
        };
    }

    /// <summary>Writes one encrypted backup file and prunes old ones. Returns the written path, or null.</summary>
    public async Task<string?> RunBackupAsync(BackupPrefs prefs)
    {
        if (string.IsNullOrWhiteSpace(prefs.Folder)) return null;
        if (_session.Key is null || _db.KdfSalt is null) return null;
        if (System.Threading.Interlocked.Exchange(ref _running, 1) == 1) return null;

        try
        {
            var now = DateTimeOffset.UtcNow;
            string? path = null;
            var written = await Task.Run(() =>
            {
                Directory.CreateDirectory(prefs.Folder);
                var entries = _db.ListEntries();
                var payloads = new List<object?>();
                foreach (var entry in entries)
                    payloads.Add(EntryKinds.IsCard(entry.Kind)
                        ? (object?)_db.DecryptCard(entry)
                        : _db.DecryptEntry(entry));

                var bytes = _export.ExportWithKey(entries, payloads, _session.Key!, _db.KdfSalt, _db.KdfIterations);
                path = Path.Combine(prefs.Folder, BackupFileName(now));
                File.WriteAllBytes(path, bytes);
                Prune(prefs.Folder, prefs.Retain);
                return true;
            });

            if (written && path is not null)
            {
                _db.SetMeta(LastKey, now.ToString("O"));
                LastModified = now;
                LastError = string.Empty;
            }
            return written ? path : null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return null;
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _running, 0);
        }
    }

    /// <summary>Deletes oldest auto-backups beyond <paramref name="retain"/>. Returns the number deleted.</summary>
    internal static int Prune(string folder, int retain)
    {
        if (retain < 1 || !Directory.Exists(folder)) return 0;
        var files = Directory.GetFiles(folder, "cardvault-auto-*.cvault")
            .OrderByDescending(Path.GetFileName)
            .Skip(Math.Max(0, retain))
            .ToList();
        foreach (var file in files)
        {
            try { File.Delete(file); }
            catch { /* a locked file on Windows must not block the backup */ }
        }
        return files.Count;
    }

    /// <summary>Zero-padded UTC wall-clock name; sorts lexicographically in time order.</summary>
    public static string BackupFileName(DateTimeOffset now) =>
        $"cardvault-auto-{now:yyyyMMdd-HHmmss}.cvault";

    public static string DisplayName(string cadence) => cadence switch
    {
        Daily => "Daily",
        Weekly => "Weekly",
        Monthly => "Monthly",
        _ => "Off",
    };

    private async Task OnTickAsync()
    {
        if (_session.Key is null) return;
        var prefs = ReadPrefs();
        if (string.IsNullOrWhiteSpace(prefs.Folder) || !IsDue(prefs.LastBackup, prefs.Cadence, DateTimeOffset.UtcNow)) return;
        await RunBackupAsync(prefs);
    }

    private static DateTimeOffset? ParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dto) ? dto : null;
}

/// <summary>User-configured backup schedule, persisted in the vault's meta table.</summary>
public sealed record BackupPrefs(string Folder, string Cadence, int Retain, DateTimeOffset? LastBackup);