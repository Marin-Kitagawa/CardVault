using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CardVault.Data;
using CardVault.Models;
using CardVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

public sealed record LockOption(int Minutes, string Label);

public sealed record ThemeOption(ThemeKind Kind, string Label, string Blurb);

public sealed record BackupCadenceOption(string Value, string Label);

public sealed record BackupRetainOption(int Count, string Label);

public partial class SettingsViewModel : ViewModelBase
{
    private readonly VaultDatabase _db;
    private readonly ExportService _export;
    private readonly KeepassImportService _keepass;
    private readonly AutoBackupService _backup;
    private readonly SyncService _sync;
    private readonly Action _onChanged;
    private bool _loaded;

    public event Action? RequestClose;

    public IReadOnlyList<LockOption> LockOptions { get; } = new List<LockOption>
    {
        new(1, "After 1 minute"),
        new(5, "After 5 minutes"),
        new(15, "After 15 minutes"),
        new(30, "After 30 minutes"),
        new(60, "After 1 hour"),
    };

    public IReadOnlyList<ThemeOption> ThemeOptions { get; } = new List<ThemeOption>
    {
        new(ThemeKind.Atelier, "Atelier", "Washi paper, sumi ink, one vermillion mark."),
        new(ThemeKind.Readout, "Readout", "Bedside clock \u2014 amber signal, seven-segment digits."),
    };

    public IReadOnlyList<BackupCadenceOption> BackupCadences { get; } = new List<BackupCadenceOption>
    {
        new(string.Empty, "Off"),
        new(AutoBackupService.Daily, "Daily"),
        new(AutoBackupService.Weekly, "Weekly"),
        new(AutoBackupService.Monthly, "Monthly"),
    };

    public IReadOnlyList<BackupRetainOption> BackupRetains { get; } = new List<BackupRetainOption>
    {
        new(7, "Keep 7"),
        new(14, "Keep 14"),
        new(30, "Keep 30"),
        new(60, "Keep 60"),
    };

    [ObservableProperty] private LockOption selectedLock;
    [ObservableProperty] private ThemeOption selectedTheme;
    [ObservableProperty] private string oldPassword = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool updateBusy;
    [ObservableProperty] private bool updateAvailable;
    [ObservableProperty] private string updateStatus = $"Running v{UpdateService.CurrentVersion}. Check GitHub releases for updates.";
    [ObservableProperty] private string updateUrl = UpdateService.ReleasesUrl;
    [ObservableProperty] private BackupCadenceOption selectedBackupCadence;
    [ObservableProperty] private BackupRetainOption selectedBackupRetain;
    [ObservableProperty] private string backupFolder = string.Empty;
    [ObservableProperty] private bool backupBusy;
    [ObservableProperty] private string backupStatus = string.Empty;
    [ObservableProperty] private bool syncEnabled;
    [ObservableProperty] private string syncFolder = string.Empty;
    [ObservableProperty] private bool syncBusy;
    [ObservableProperty] private string syncStatus = string.Empty;

    public SettingsViewModel(VaultDatabase db, ExportService export, AutoBackupService backup, SyncService sync, Action onChanged)
    {
        _db = db;
        _export = export;
        _backup = backup;
        _sync = sync;
        _keepass = new KeepassImportService(db);
        _onChanged = onChanged;
        SelectedLock = LockOptions.FirstOrDefault(x => x.Minutes == db.LockTimeoutMinutes) ?? LockOptions[1];
        SelectedTheme = ThemeOptions.FirstOrDefault(x => x.Kind == ThemeService.Current) ?? ThemeOptions[0];

        var prefs = _backup.ReadPrefs();
        SelectedBackupCadence = BackupCadences.FirstOrDefault(x => x.Value == prefs.Cadence) ?? BackupCadences[0];
        SelectedBackupRetain = BackupRetains.FirstOrDefault(x => x.Count == prefs.Retain) ?? BackupRetains[1];
        BackupFolder = prefs.Folder;

        var syncPrefs = _sync.ReadPrefs();
        SyncEnabled = syncPrefs.Enabled;
        SyncFolder = syncPrefs.Folder;
        _loaded = true;
        RefreshBackupStatus();
        RefreshSyncStatus();
    }

    public bool IsIdle => !IsBusy;
    public bool IsUpdateIdle => !UpdateBusy;
    public bool IsBackupIdle => !BackupBusy;
    public bool IsSyncIdle => !SyncBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsIdle));
    partial void OnUpdateBusyChanged(bool value) => OnPropertyChanged(nameof(IsUpdateIdle));
    partial void OnBackupBusyChanged(bool value) => OnPropertyChanged(nameof(IsBackupIdle));
    partial void OnSyncBusyChanged(bool value) => OnPropertyChanged(nameof(IsSyncIdle));

    partial void OnSelectedLockChanged(LockOption value) => _db.LockTimeoutMinutes = value.Minutes;

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (value.Kind != ThemeService.Current)
            ThemeService.Apply(value.Kind, _db);
    }

    partial void OnSelectedBackupCadenceChanged(BackupCadenceOption value)
    {
        if (!_loaded || value is null) return;
        PersistBackupPrefs();
        RefreshBackupStatus();
    }

    partial void OnSelectedBackupRetainChanged(BackupRetainOption value)
    {
        if (!_loaded || value is null) return;
        PersistBackupPrefs();
    }

    private void PersistBackupPrefs() => _backup.SavePrefs(new BackupPrefs(
        BackupFolder.Trim(),
        SelectedBackupCadence?.Value ?? string.Empty,
        SelectedBackupRetain?.Count ?? AutoBackupService.DefaultRetain,
        null));

    private void RefreshBackupStatus()
    {
        var prefs = _backup.ReadPrefs();
        var when = prefs.LastBackup is null
            ? "No automatic backup has run yet."
            : $"Last backup: {prefs.LastBackup.Value.LocalDateTime:g}.";
        BackupStatus = prefs.Folder.Length == 0
            ? when + " Choose a folder to enable automatic backups."
            : when + (prefs.Cadence.Length == 0
                ? " Schedule is off — backups will not run until you pick a cadence."
                : $" Runs {AutoBackupService.DisplayName(prefs.Cadence).ToLowerInvariant()}, keeping the newest {prefs.Retain}.");
        if (_backup.LastError.Length > 0)
            BackupStatus += "\nLast attempt failed: " + _backup.LastError;
    }

    [RelayCommand]
    private async Task PickBackupFolderAsync()
    {
        var owner = AppServices.MainWindow;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose automatic backup folder",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            await DialogService.ShowAsync(owner, "Folder unavailable", "That location could not be used.");
            return;
        }

        BackupFolder = path;
        PersistBackupPrefs();
        RefreshBackupStatus();
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        var owner = AppServices.MainWindow;
        PersistBackupPrefs();
        var prefs = _backup.ReadPrefs();
        if (prefs.Folder.Length == 0)
        {
            await DialogService.ShowAsync(owner, "No backup folder", "Choose a folder first — the backup file has to live somewhere.");
            return;
        }

        BackupBusy = true;
        try
        {
            var path = await _backup.RunBackupAsync(prefs);
            if (path is null)
            {
                if (_backup.LastError.Length > 0)
                    await DialogService.ShowAsync(owner, "Backup failed", _backup.LastError);
                else
                    await DialogService.ShowAsync(owner, "Backup skipped", "The vault is locked. Unlock it and try again.");
            }
            else
            {
                await DialogService.ShowAsync(owner, "Backup complete",
                    $"Encrypted backup written to:\n{path}\n\nIt can be restored with your master password on any CardVault installation.");
            }
        }
        finally
        {
            BackupBusy = false;
            RefreshBackupStatus();
        }
    }

    // ============================= sync =============================

    private void PersistSyncPrefs()
    {
        var folder = SyncFolder.Trim();
        _sync.SavePrefs(new SyncPrefs(folder, SyncEnabled, null));
    }

    partial void OnSyncEnabledChanged(bool value)
    {
        if (!_loaded) return;
        PersistSyncPrefs();
        if (value && SyncFolder.Length == 0)
            SyncStatus = "Sync is on, but no folder is chosen yet.";
        else
            RefreshSyncStatus();
    }

    private void RefreshSyncStatus()
    {
        var prefs = _sync.ReadPrefs();
        var when = prefs.LastSync is null
            ? "No sync has run yet."
            : $"Last sync: {prefs.LastSync.Value.LocalDateTime:g}.";
        if (prefs.Folder.Length == 0)
        {
            SyncStatus = when + " Sync is off — choose a folder to enable it.";
        }
        else if (!prefs.Enabled)
        {
            SyncStatus = when + $" Folder: {prefs.Folder} — sync is disabled.";
        }
        else
        {
            SyncStatus = when + " Changes are exchanged every few minutes while the vault is unlocked.\n" +
                "The sync file is encrypted with your master password — both devices must know it.";
        }
        if (_sync.LastError.Length > 0)
            SyncStatus += "\nLast attempt failed: " + _sync.LastError;
    }

    [RelayCommand]
    private async Task PickSyncFolderAsync()
    {
        var owner = AppServices.MainWindow;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose shared sync folder (Dropbox, OneDrive, network drive…)",
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;

        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            await DialogService.ShowAsync(owner, "Folder unavailable", "That location could not be used.");
            return;
        }

        if (_sync.AdoptRemoteSalt(path))
            SyncStatus = "Picked the shared folder. If needed, unlock again so this device adopts the sync key from the snapshot.";
        SyncFolder = path;
        PersistSyncPrefs();
        RefreshSyncStatus();
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        var owner = AppServices.MainWindow;
        PersistSyncPrefs();
        if (SyncFolder.Length == 0)
        {
            await DialogService.ShowAsync(owner, "No sync folder", "Choose a shared folder first — the sync snapshot has to live somewhere.");
            return;
        }

        SyncBusy = true;
        try
        {
            var touched = await _sync.RunSyncAsync();
            if (touched)
            {
                _onChanged?.Invoke();
                await DialogService.ShowAsync(owner, "Sync complete",
                    "The vault is now in step with the shared folder.");
            }
            else if (_sync.LastError.Length > 0)
            {
                await DialogService.ShowAsync(owner, "Sync failed", _sync.LastError);
            }
            else
            {
                await DialogService.ShowAsync(owner, "Sync complete",
                    "Nothing to change — the vault was already in step.");
            }
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(owner, "Sync failed", ex.Message);
        }
        finally
        {
            SyncBusy = false;
            RefreshSyncStatus();
        }
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (NewPassword.Length < 8)
        {
            await DialogService.ShowAsync(AppServices.MainWindow, "Password too short", "Use at least 8 characters.");
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            await DialogService.ShowAsync(AppServices.MainWindow, "Passwords differ", "The new password and its confirmation do not match.");
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() => _db.ChangePassword(NewPassword));
            OldPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
            await DialogService.ShowAsync(AppServices.MainWindow, "Password updated",
                "Your master password has been changed and every entry was re-encrypted.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(AppServices.MainWindow, "Could not change password", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        var owner = AppServices.MainWindow;
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export encrypted vault",
            SuggestedFileName = $"cardvault-backup-{DateTime.Now:yyyyMMdd-HHmmss}",
            DefaultExtension = ExportService.FileExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CardVault encrypted backup") { Patterns = new[] { $"*.{ExportService.FileExtension}" } },
            },
        });
        if (file is null) return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            await DialogService.ShowAsync(owner, "Export cancelled", "The file location could not be used.");
            return;
        }

        var passphrase = await DialogService.AskPassphraseAsync(owner, "Export passphrase",
            "Choose a passphrase to protect this backup. It can be different from your master password and does not need it.",
            requireConfirm: true);
        if (passphrase is null) return;

        IsBusy = true;
        try
        {
            var bytes = await Task.Run(() =>
            {
                var entries = _db.ListEntries();
                var payloads = new List<object?>();
                foreach (var entry in entries)
                    payloads.Add(EntryKinds.IsCard(entry.Kind)
                        ? (object?)_db.DecryptCard(entry)
                        : _db.DecryptEntry(entry));
                return _export.Export(entries, payloads, passphrase);
            });

            await File.WriteAllBytesAsync(path, bytes);
            await DialogService.ShowAsync(owner, "Export complete",
                $"Encrypted backup written to:\n{path}\n\nKeep the passphrase safe — without it the backup cannot be restored.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(owner, "Export failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var owner = AppServices.MainWindow;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import CardVault backup",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CardVault encrypted backup") { Patterns = new[] { $"*.{ExportService.FileExtension}" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });
        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var passphrase = await DialogService.AskPassphraseAsync(owner, "Import passphrase",
            "Enter the passphrase that protects this backup file.",
            requireConfirm: false);
        if (passphrase is null) return;

        IsBusy = true;
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var count = await Task.Run(() =>
            {
                var items = _export.Import(bytes, passphrase);
                foreach (var item in items)
                {
                    var entry = item.Entry;
                    if (item.Payload is CardSecureData card)
                        _db.SaveImported(entry, entry.Name, entry.Kind, entry.Brand, entry.Accent, card, null);
                    else if (item.Payload is EntrySecureData generic)
                        _db.SaveImported(entry, entry.Name, entry.Kind, entry.Brand, entry.Accent, null, generic);
                }
                return items.Count;
            });

            _onChanged?.Invoke();
            await DialogService.ShowAsync(owner, "Import complete",
                count == 1 ? "Imported 1 item." : $"Imported {count} items.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(owner, "Import failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportKeepassAsync()
    {
        var owner = AppServices.MainWindow;
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import KeePass database",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("KeePass 2 database") { Patterns = new[] { "*.kdbx", "*.kdb" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });
        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var passphrase = await DialogService.AskPassphraseAsync(owner, "KeePass password",
            "Enter the password that opens this KeePass database. Only password-protected KDBX 3.x files are supported.",
            requireConfirm: false);
        if (passphrase is null) return;

        IsBusy = true;
        try
        {
            var result = await Task.Run(() =>
            {
                var bytes = File.ReadAllBytes(path);
                return _keepass.Import(bytes, passphrase);
            });

            _onChanged?.Invoke();
            await DialogService.ShowAsync(owner, "Import complete",
                $"Imported {result.Entries} item{(result.Entries == 1 ? "" : "s")} into {result.Folders} folder{(result.Folders == 1 ? "" : "s")}.\n\nGroups become folders, and entries become items. KeePass passwords and secrets are stored inside your encrypted vault like any other entry.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(owner, "KeePass import failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var owner = AppServices.MainWindow;
        var proceed = await DialogService.ConfirmAsync(owner, "Plain-text export",
            "A CSV file is PLAINTEXT — it contains your decrypted values, including any secrets, readable by anyone who opens the file.\n\nThis is different from the encrypted backup. Export anyway?",
            "Export CSV", "Cancel", danger: true);
        if (!proceed) return;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export as CSV",
            SuggestedFileName = $"cardvault-{DateTime.Now:yyyyMMdd-HHmmss}",
            DefaultExtension = CsvExporter.FileExtension,
            FileTypeChoices = new[]
            {
                new FilePickerFileType("CSV spreadsheet") { Patterns = new[] { $"*.{CsvExporter.FileExtension}" } },
            },
        });
        if (file is null) return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            await DialogService.ShowAsync(owner, "Export cancelled", "The file location could not be used.");
            return;
        }

        IsBusy = true;
        try
        {
            var bytes = await Task.Run(() =>
            {
                var entries = _db.ListEntries();
                var payloads = new List<object?>();
                foreach (var entry in entries)
                    payloads.Add(EntryKinds.IsCard(entry.Kind)
                        ? (object?)_db.DecryptCard(entry)
                        : _db.DecryptEntry(entry));
                return CsvExporter.Export(entries, payloads);
            });

            await File.WriteAllBytesAsync(path, bytes);
            await DialogService.ShowAsync(owner, "Export complete",
                $"Plain-text CSV written to:\n{path}\n\nTreat this file like a password — it is not encrypted.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(owner, "Export failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var owner = AppServices.MainWindow;
        var proceed = await DialogService.ConfirmAsync(owner, "Plain-text import",
            "This CSV file is PLAINTEXT — it is NOT encrypted. Only import CSV files you created yourself (e.g. via Settings ▸ Export plain CSV) or from a source you fully trust.\n\nItems will be added to your vault as soon as you confirm. Import anyway?",
            "Import CSV", "Cancel", danger: true);
        if (!proceed) return;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import CSV",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSV spreadsheet") { Patterns = new[] { $"*.{CsvExporter.FileExtension}" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*" } },
            },
        });
        if (files.Count == 0) return;

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
        {
            await DialogService.ShowAsync(owner, "Import cancelled", "The file location could not be used.");
            return;
        }

        IsBusy = true;
        try
        {
            var count = await Task.Run(() =>
            {
                var bytes = File.ReadAllBytes(path);
                var items = CsvImporter.Parse(bytes);
                foreach (var item in items)
                {
                    var entry = item.Entry;
                    if (item.Payload is CardSecureData card)
                        _db.SaveImported(entry, entry.Name, entry.Kind, entry.Brand, entry.Accent, card, null);
                    else if (item.Payload is EntrySecureData generic)
                        _db.SaveImported(entry, entry.Name, entry.Kind, entry.Brand, entry.Accent, null, generic);
                }
                return items.Count;
            });

            _onChanged?.Invoke();
            await DialogService.ShowAsync(owner, "Import complete",
                count == 1 ? "Imported 1 item." : $"Imported {count} items.");
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(owner, "Import failed", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        UpdateBusy = true;
        try
        {
            var result = await Task.Run(() => UpdateService.CheckAsync().GetAwaiter().GetResult());
            if (result.Error is not null)
            {
                UpdateAvailable = false;
                UpdateStatus = $"Could not check for updates: {result.Error}";
            }
            else if (result.HasUpdate && result.Latest is not null)
            {
                UpdateAvailable = true;
                UpdateUrl = result.Page ?? UpdateService.ReleasesUrl;
                UpdateStatus = $"A newer version (v{result.Latest}) is available. You are running v{UpdateService.CurrentVersion}.";
            }
            else
            {
                UpdateAvailable = false;
                UpdateStatus = $"You are up to date — running v{UpdateService.CurrentVersion}.";
            }
        }
        finally
        {
            UpdateBusy = false;
        }
    }

    [RelayCommand]
    private void OpenUpdatePage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateUrl) { UseShellExecute = true });
        }
        catch
        {
            // No browser available; the status text already shows the version.
        }
    }

    [RelayCommand]
    private void LockNow() => AppServices.Session.Lock();

    [RelayCommand]
    private void Done() => RequestClose?.Invoke();
}