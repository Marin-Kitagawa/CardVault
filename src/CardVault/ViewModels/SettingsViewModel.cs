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

public partial class SettingsViewModel : ViewModelBase
{
    private readonly VaultDatabase _db;
    private readonly ExportService _export;
    private readonly Action _onChanged;

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

    [ObservableProperty] private LockOption selectedLock;
    [ObservableProperty] private ThemeOption selectedTheme;
    [ObservableProperty] private string oldPassword = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmPassword = string.Empty;
    [ObservableProperty] private bool isBusy;

    public SettingsViewModel(VaultDatabase db, ExportService export, Action onChanged)
    {
        _db = db;
        _export = export;
        _onChanged = onChanged;
        SelectedLock = LockOptions.FirstOrDefault(x => x.Minutes == db.LockTimeoutMinutes) ?? LockOptions[1];
        SelectedTheme = ThemeOptions.FirstOrDefault(x => x.Kind == ThemeService.Current) ?? ThemeOptions[0];
    }

    public bool IsIdle => !IsBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    partial void OnSelectedLockChanged(LockOption value) => _db.LockTimeoutMinutes = value.Minutes;

    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (value.Kind != ThemeService.Current)
            ThemeService.Apply(value.Kind, _db);
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
    private void LockNow() => AppServices.Session.Lock();

    [RelayCommand]
    private void Done() => RequestClose?.Invoke();
}