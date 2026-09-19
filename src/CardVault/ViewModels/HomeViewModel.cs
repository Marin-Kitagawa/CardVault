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
    private List<Folder> _folders = new();
    private readonly HashSet<string> _expanded = new();
    private Dictionary<string, List<Folder>> _children = new();
    private List<string> _descendants = new();

    public ObservableCollection<EntryTileViewModel> Entries { get; } = new();
    public ObservableCollection<FolderNodeViewModel> FolderTree { get; } = new();

    [ObservableProperty]
    private string subtitleText = "Your encrypted wallet";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string selectedFolderId = string.Empty;

    public string Subtitle => SubtitleText;

    public bool ShowEmpty => Entries.Count == 0;
    public bool HasItems => _all.Count > 0;
    public string EmptyTitle => HasItems ? "No matches" : "No entries yet";
    public string EmptyBlurb => HasItems
        ? "Try a different search - nothing matched."
        : "Everything stays encrypted with your master password.";
    public string AddButtonLabel => HasItems ? "Add an entry" : "Add your first entry";

    public void Refresh()
    {
        _all.Clear();
        Entries.Clear();

        if (!AppServices.Session.IsUnlocked) return;

        _folders = AppServices.Database.ListFolders();
        _children = BuildChildren(_folders);
        _descendants = SelectedFolderId.Length == 0
            ? new List<string>()
            : FolderHelpers.Descendants(_folders, SelectedFolderId).ToList();

        foreach (var entry in AppServices.Database.ListEntries())
            _all.Add(new EntryTileViewModel(entry, OpenEntry));

        var count = _all.Count;
        SubtitleText = count == 0
            ? "Your encrypted wallet"
            : count == 1
                ? "1 item secured"
                : $"{count} items secured";

        if (SelectedFolderId.Length > 0)
        {
            var folder = _folders.FirstOrDefault(f => f.Id == SelectedFolderId);
            if (folder is not null)
                SubtitleText = $"Showing {_descendants.Count + 1} folder(s) - {folder.Name}";
        }

        RebuildFolderTree();
        ApplyFilter();

        OnPropertyChanged(nameof(HasItems));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedFolderIdChanged(string value)
    {
        _descendants = value.Length == 0
            ? new List<string>()
            : FolderHelpers.Descendants(_folders, value).ToList();
        ApplyFilter();
        UpdateFolderSelection();
    }

    private void ApplyFilter()
    {
        Entries.Clear();

        var query = SearchText.Trim();
        var folderScoped = SelectedFolderId.Length > 0;
        var scope = new HashSet<string>(_descendants) { SelectedFolderId };

        IEnumerable<EntryTileViewModel> source = _all;
        if (folderScoped)
            source = source.Where(t => scope.Contains(t.FolderId));

        if (query.Length > 0)
            source = source.Where(Match);

        foreach (var tile in source)
            Entries.Add(tile);

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

    // ============================= folder tree =============================

    private static Dictionary<string, List<Folder>> BuildChildren(List<Folder> folders)
    {
        var map = new Dictionary<string, List<Folder>>();
        foreach (var folder in folders)
        {
            if (!map.TryGetValue(folder.ParentId, out var list))
                map[folder.ParentId] = list = new List<Folder>();
            list.Add(folder);
        }
        return map;
    }

    private Dictionary<string, int> SubtreeCounts()
    {
        var counts = new Dictionary<string, int>();
        foreach (var folder in _folders)
        {
            var n = AppServices.Database.ListEntriesByFolder(folder.Id).Count;
            var subtree = FolderHelpers.Descendants(_folders, folder.Id);
            foreach (var sub in subtree)
                n += AppServices.Database.ListEntriesByFolder(sub).Count;
            counts[folder.Id] = n;
        }
        return counts;
    }

    private void RebuildFolderTree()
    {
        FolderTree.Clear();
        var counts = SubtreeCounts();

        void Add(Folder folder, int depth)
        {
            var isExpanded = folder.Id == SelectedFolderId || _expanded.Contains(folder.Id);
            var hasChildren = _children.TryGetValue(folder.Id, out var kids) && kids.Count > 0;
            FolderTree.Add(new FolderNodeViewModel(
                folder, depth, hasChildren,
                counts.TryGetValue(folder.Id, out var c) ? c : 0,
                isExpanded, folder.Id == SelectedFolderId,
                ToggleFolder, SelectFolder));
        }

        void Walk(string parentId, int depth)
        {
            if (!_children.TryGetValue(parentId, out var list)) return;
            foreach (var folder in list)
            {
                Add(folder, depth);
                if (_expanded.Contains(folder.Id) || folder.Id == SelectedFolderId)
                    Walk(folder.Id, depth + 1);
            }
        }

        Walk(string.Empty, 0);

        if (FolderTree.Count == 0)
        {
            var emptyFolderRow = new FolderNodeViewModel(
                new Folder { Id = string.Empty, Name = "No folders yet" }, 0, false, 0, false, false,
                _ => { }, _ => { });
            FolderTree.Add(emptyFolderRow);
        }
    }

    private void ToggleFolder(FolderNodeViewModel node)
    {
        if (_expanded.Contains(node.Id))
            _expanded.Remove(node.Id);
        else
            _expanded.Add(node.Id);
        RebuildFolderTree();
    }

    private void SelectFolder(FolderNodeViewModel node)
    {
        if (node.Id.Length == 0) return;
        SelectedFolderId = node.Id;
        ApplyFilter();
        UpdateFolderSelection();
    }

    private void UpdateFolderSelection()
    {
        foreach (var row in FolderTree)
            row.SetSelected(row.Id == SelectedFolderId);
    }

    [RelayCommand]
    private void ShowAll()
    {
        SelectedFolderId = string.Empty;
        UpdateFolderSelection();
        ApplyFilter();
    }

    [RelayCommand]
    private void ManageFolders()
    {
        var vm = new FolderManagerViewModel();
        var window = new FolderManagerWindow { DataContext = vm };
        window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    [RelayCommand]
    private void AddFolder()
    {
        var vm = new FolderEditorViewModel(parentId: SelectedFolderId);
        var window = new FolderEditorWindow { DataContext = vm };
        window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    // ============================= entries =============================

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
        var vm = new SettingsViewModel(AppServices.Database, AppServices.Export, AppServices.AutoBackup, AppServices.Sync, Refresh);
        var window = new SettingsWindow { DataContext = vm };
        await window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    [RelayCommand]
    private void Lock() => AppServices.Session.Lock();
}