using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CardVault.Models;
using CardVault.Services;
using CardVault.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

public partial class HomeViewModel : ViewModelBase
{
    private readonly List<EntryTileViewModel> _all = new();

    public ObservableCollection<EntryTileViewModel> Entries { get; } = new();

    [ObservableProperty]
    private string subtitleText = "Your encrypted wallet";

    [ObservableProperty]
    private string searchText = string.Empty;

    public string Subtitle => SubtitleText;

    public bool ShowEmpty => Entries.Count == 0;
    public bool HasItems => _all.Count > 0;
    public string EmptyTitle => HasItems ? "No matches" : "No entries yet";
    public string EmptyBlurb => HasItems
        ? "Try a different search — nothing matched."
        : "Everything stays encrypted with your master password.";
    public string AddButtonLabel => HasItems ? "Add an entry" : "Add your first entry";

    public void Refresh()
    {
        _all.Clear();
        Entries.Clear();

        if (!AppServices.Session.IsUnlocked) return;

        foreach (var entry in AppServices.Database.ListEntries())
            _all.Add(new EntryTileViewModel(entry, OpenEntry));

        var count = _all.Count;
        SubtitleText = count == 0
            ? "Your encrypted wallet"
            : count == 1
                ? "1 item secured"
                : $"{count} items secured";

        ApplyFilter();

        OnPropertyChanged(nameof(HasItems));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        Entries.Clear();

        var query = SearchText.Trim();
        if (query.Length == 0)
        {
            foreach (var tile in _all) Entries.Add(tile);
        }
        else
        {
            foreach (var tile in _all.Where(Match))
                Entries.Add(tile);
        }

        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyBlurb));
        OnPropertyChanged(nameof(Subtitle));
    }

    private bool Match(EntryTileViewModel tile)
    {
        var q = SearchText.Trim();
        return tile.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
            || tile.KindName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || tile.BrandLabel.Contains(q, StringComparison.OrdinalIgnoreCase)
            || tile.Tags.Contains(q, StringComparison.OrdinalIgnoreCase);
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