using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CardVault.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

/// <summary>
/// Create or edit a folder: name, parent and icon from the library. Used both by
/// the quick-create flow and the folder manager.
/// </summary>
public partial class FolderEditorViewModel : ViewModelBase
{
    private readonly Folder? _existing;

    public event Action? RequestClose;

    [ObservableProperty] private string folderName = string.Empty;
    [ObservableProperty] private FolderOptionViewModel? selectedParent;
    [ObservableProperty] private bool canSave;

    public ObservableCollection<FolderOptionViewModel> ParentOptions { get; } = new();
    public ObservableCollection<FolderIconPickViewModel> Icons { get; } = new();
    public IReadOnlyList<string> Categories => FolderIcons.Categories;

    public string Title => _existing is null ? "New folder" : "Rename folder";
    public string SaveLabel => _existing is null ? "Create" : "Save changes";

    public FolderEditorViewModel(Folder? existing = null, string parentId = "")
    {
        _existing = existing;
        FolderName = existing?.Name ?? string.Empty;
        ParentOptions.Clear();
        foreach (var option in FolderOptionBuilder.Flatten(
                     AppServices.Database.ListFolders(),
                     excludeId: existing?.Id,
                     noneLabel: "Top level (no parent)"))
        {
            ParentOptions.Add(option);
        }

        SelectedParent = ParentOptions.FirstOrDefault(o =>
                              o.Id == (existing?.ParentId ?? parentId)) ?? ParentOptions[0];

        foreach (var icon in FolderIcons.All)
        {
            var vm = new FolderIconPickViewModel(icon, _ => SelectIcon(icon.Key));
            if (icon.Key == (existing?.Icon ?? "folder")) vm.IsSelected = true;
            Icons.Add(vm);
        }

        CanSave = FolderName.Trim().Length > 0;
    }

    public string IconKey
    {
        get
        {
            var selected = Icons.FirstOrDefault(i => i.IsSelected);
            return selected?.Key ?? "folder";
        }
    }

    partial void OnFolderNameChanged(string value)
        => CanSave = !string.IsNullOrWhiteSpace(value.Trim());

    private void SelectIcon(string key)
    {
        foreach (var icon in Icons)
            icon.IsSelected = icon.Key == key;
        OnPropertyChanged(nameof(IconKey));
    }

    [RelayCommand]
    private void Save()
    {
        var name = FolderName.Trim();
        if (name.Length == 0) return;

        var parent = SelectedParent?.Id ?? string.Empty;
        if (_existing is null)
        {
            AppServices.Database.CreateFolder(name, IconKey, parent);
        }
        else
        {
            _existing.Name = name;
            _existing.Icon = IconKey;
            _existing.ParentId = parent;
            AppServices.Database.UpdateFolder(_existing);
        }

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}