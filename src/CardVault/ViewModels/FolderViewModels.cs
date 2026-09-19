using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using CardVault.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

/// <summary>
/// A single row in the Home sidebar or the folder manager. Rows are flattened
/// with indentation (Depth) so an ItemsControl can render nesting cheaply.
/// </summary>
public partial class FolderNodeViewModel : ViewModelBase
{
    private readonly Action<FolderNodeViewModel> _onToggle;
    private readonly Action<FolderNodeViewModel> _onSelect;

    public FolderNodeViewModel(
        Folder folder,
        int depth,
        bool hasChildren,
        int totalCount,
        bool isExpanded,
        bool isSelected,
        Action<FolderNodeViewModel> onToggle,
        Action<FolderNodeViewModel> onSelect)
    {
        Folder = folder;
        Depth = depth;
        IsExpandable = hasChildren;
        TotalCount = totalCount;
        IsExpanded = isExpanded;
        IsSelected = isSelected;
        _onToggle = onToggle;
        _onSelect = onSelect;
        Icon = FolderIcons.GetGeometry(folder.Icon);
        ToggleCommand = new RelayCommand(() => _onToggle(this));
        SelectCommand = new RelayCommand(() => _onSelect(this));
    }

    public Folder Folder { get; }
    public string Id => Folder.Id;
    public string Name => Folder.Name;
    public int Depth { get; }
    public bool IsExpandable { get; }
    public int TotalCount { get; }
    public string CountText => TotalCount > 0 ? TotalCount.ToString() : string.Empty;
    public string Chevron => IsExpanded ? "\u25BE" : "\u25B8";
    public Thickness Indent => new(Depth * 16, 0, 0, 0);
    public StreamGeometry Icon { get; }

    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private bool isSelected;

    public IRelayCommand ToggleCommand { get; }
    public IRelayCommand SelectCommand { get; }

    public void SetSelected(bool selected) => IsSelected = selected;
}

/// <summary>Flattened option for folder pickers (entry form, editor parent list).</summary>
public sealed class FolderOptionViewModel
{
    public FolderOptionViewModel(string id, string label)
    {
        Id = id;
        Label = label;
    }

    public string Id { get; }
    public string Label { get; }
}

/// <summary>One selectable icon in the folder editor's icon library grid.</summary>
public partial class FolderIconPickViewModel : ViewModelBase
{
    private readonly Action<FolderIconPickViewModel> _onPick;

    public FolderIconPickViewModel(FolderIconInfo info, Action<FolderIconPickViewModel> onPick)
    {
        Info = info;
        _onPick = onPick;
        Icon = FolderIcons.GetGeometry(info.Key);
        SelectCommand = new RelayCommand(() => _onPick(this));
    }

    public FolderIconInfo Info { get; }
    public string Key => Info.Key;
    public string Name => Info.Name;
    public string Category => Info.Category;
    public StreamGeometry Icon { get; }

    [ObservableProperty] private bool isSelected;

    public IRelayCommand SelectCommand { get; }
}

/// <summary>Row in the folder manager window.</summary>
public partial class FolderManagerRowViewModel : ViewModelBase
{
    private readonly Action<FolderManagerRowViewModel> _onEdit;
    private readonly Action<FolderManagerRowViewModel> _onDelete;

    public FolderManagerRowViewModel(
        Folder folder,
        int depth,
        int totalCount,
        Action<FolderManagerRowViewModel> onEdit,
        Action<FolderManagerRowViewModel> onDelete)
    {
        Folder = folder;
        Depth = depth;
        TotalCount = totalCount;
        _onEdit = onEdit;
        _onDelete = onDelete;
        Icon = FolderIcons.GetGeometry(folder.Icon);
        Indent = new Thickness(depth * 16, 0, 0, 0);
        EditCommand = new RelayCommand(() => _onEdit(this));
        DeleteCommand = new RelayCommand(() => _onDelete(this));
    }

    public Folder Folder { get; }
    public string Id => Folder.Id;
    public string Name => Folder.Name;
    public int Depth { get; }
    public int TotalCount { get; }
    public string CountText => TotalCount > 0 ? TotalCount.ToString() : string.Empty;
    public Thickness Indent { get; }
    public StreamGeometry Icon { get; }
    public IRelayCommand EditCommand { get; }
    public IRelayCommand DeleteCommand { get; }
}

/// <summary>Static helper that turns a folder set into indented option rows.</summary>
public static class FolderOptionBuilder
{
    public static List<FolderOptionViewModel> Flatten(
        List<Folder> folders,
        string? excludeId = null,
        string noneLabel = "No folder")
    {
        var options = new List<FolderOptionViewModel> { new(string.Empty, noneLabel) };
        var children = new Dictionary<string, List<Folder>>();
        foreach (var folder in folders)
        {
            if (!children.TryGetValue(folder.ParentId, out var list))
                children[folder.ParentId] = list = new List<Folder>();
            list.Add(folder);
        }

        var blocked = FolderHelpers.Descendants(folders, excludeId ?? string.Empty);
        if (excludeId is not null && excludeId.Length > 0)
            blocked.Add(excludeId);
        void Walk(string parentId, int depth)
        {
            if (!children.TryGetValue(parentId, out var list)) return;
            foreach (var folder in list)
            {
                if (blocked.Contains(folder.Id)) continue;
                var indent = new string('\u00A0', depth * 3);
                options.Add(new FolderOptionViewModel(folder.Id, indent + folder.Name));
                Walk(folder.Id, depth + 1);
            }
        }

        Walk(string.Empty, 0);
        return options;
    }
}

/// <summary>Tiny folder-graph helpers (descendant lookups).</summary>
public static class FolderHelpers
{
    public static HashSet<string> Descendants(IReadOnlyList<Folder> folders, string rootId)
    {
        var children = new Dictionary<string, List<string>>();
        foreach (var folder in folders)
        {
            if (!children.TryGetValue(folder.ParentId, out var list))
                children[folder.ParentId] = list = new List<string>();
            list.Add(folder.Id);
        }

        var result = new HashSet<string>();
        void Walk(string id)
        {
            if (!children.TryGetValue(id, out var kids)) return;
            foreach (var kid in kids)
            {
                result.Add(kid);
                Walk(kid);
            }
        }

        Walk(rootId);
        return result;
    }
}