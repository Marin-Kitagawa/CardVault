using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CardVault.Models;
using CardVault.Services;
using CardVault.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    public ObservableCollection<EntryTileViewModel> Entries { get; } = new();

    [ObservableProperty]
    private string subtitleText = "Your encrypted wallet";

    public string Subtitle => SubtitleText;

    public bool ShowEmpty => Entries.Count == 0;

    public void Refresh()
    {
        Entries.Clear();

        if (!AppServices.Session.IsUnlocked) return;

        foreach (var entry in AppServices.Database.ListEntries())
            Entries.Add(new EntryTileViewModel(entry, OpenEntry));

        SubtitleText = Entries.Count == 0
            ? "Your encrypted wallet"
            : Entries.Count == 1
                ? "1 item secured"
                : $"{Entries.Count} items secured";

        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(Subtitle));
    }

    [RelayCommand]
    private async Task AddEntry()
    {
        var picker = new KindPickerViewModel();
        var pickerWindow = new KindPickerWindow { DataContext = picker };
        EntryKind? chosen = null;
        picker.Picked += kind => chosen = kind;
        await pickerWindow.ShowDialog(AppServices.MainWindow);
        if (chosen is null) return;

        var vm = new EntryFormViewModel(null, chosen.Value);
        var window = new EntryFormWindow { DataContext = vm };
        await window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenEntry(EntryTileViewModel tile)
    {
        var entry = AppServices.Database.GetEntry(tile.Id);
        if (entry is null) { Refresh(); return; }

        object payload;
        try
        {
            payload = EntryKinds.IsCard(entry.Kind)
                ? AppServices.Database.DecryptCard(entry)
                : (object)AppServices.Database.DecryptEntry(entry);
        }
        catch (Exception ex)
        {
            await DialogService.ShowAsync(AppServices.MainWindow, "Unable to open this entry", ex.Message);
            return;
        }

        var vm = new EntryDetailsViewModel(entry, payload);
        var window = new EntryDetailsWindow { DataContext = vm };
        await window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    [RelayCommand]
    private async Task OpenSettings()
    {
        var vm = new SettingsViewModel(AppServices.Database, AppServices.Export, Refresh);
        var window = new SettingsWindow { DataContext = vm };
        await window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    [RelayCommand]
    private void Lock() => AppServices.Session.Lock();
}