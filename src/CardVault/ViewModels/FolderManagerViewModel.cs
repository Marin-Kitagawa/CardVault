using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CardVault.Models;
using CardVault.Services;
using CardVault.Views;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

/// <summary>
/// Folder manager: lists every folder flattened with indentation and offers
/// create, edit (rename / re-parent / icon) and delete. Deleting a folder moves
/// its contents and subfolders one level up so nothing is lost.
/// </summary>
public partial class FolderManagerViewModel : ViewModelBase
{
    private List<Folder> _folders = new();

    public event Action? RequestClose;

    public ObservableCollection<FolderManagerRowViewModel> Rows { get; } = new();

    public void Refresh()
    {
        Rows.Clear();
        _folders = AppServices.Database.ListFolders();
        var counts = EntryCountsPerFolder(_folders);

        var children = new Dictionary<string, List<Folder>>();
        foreach (var folder in _folders)
        {
            if (!children.TryGetValue(folder.ParentId, out var list))
                children[folder.ParentId] = list = new List<Folder>();
            list.Add(folder);
        }

        void Walk(string parentId, int depth)
        {
            if (!children.TryGetValue(parentId, out var list)) return;
            foreach (var folder in list)
            {
                var subtree = FolderHelpers.Descendants(_folders, folder.Id);
                var total = counts[folder.Id] + subtree.Sum(sub => counts.TryGetValue(sub, out var c) ? c : 0);
                var row = new FolderManagerRowViewModel(
                    folder, depth, total, Edit, Delete);
                Rows.Add(row);
                Walk(folder.Id, depth + 1);
            }
        }

        Walk(string.Empty, 0);
    }

    private static Dictionary<string, int> EntryCountsPerFolder(IEnumerable<Folder> folders)
    {
        var counts = new Dictionary<string, int>();
        foreach (var folder in folders)
        {
            var entries = AppServices.Database.ListEntriesByFolder(folder.Id);
            counts[folder.Id] = entries.Count;
        }

        return counts;
    }

    [RelayCommand]
    private void NewFolder()
    {
        var vm = new FolderEditorViewModel();
        var window = new FolderEditorWindow { DataContext = vm };
        window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    /// <summary>Re-parents a folder (drag-and-drop). No-ops when it would create a cycle.</summary>
    public void AssignParent(string folderId, string parentId)
    {
        var folders = AppServices.Database.ListFolders();
        if (!CanAssign(folders, folderId, parentId)) return;
        AppServices.Database.MoveFolder(folderId, parentId);
        Refresh();
    }

    /// <summary>
    /// Pure guard for re-parenting: a folder cannot become its own child nor a
    /// child of one of its own descendants (that would form a cycle).
    /// </summary>
    public static bool CanAssign(IReadOnlyList<Folder> folders, string childId, string parentId)
    {
        if (parentId.Length == 0) return true;
        if (string.Equals(childId, parentId, StringComparison.Ordinal)) return false;
        return !FolderHelpers.Descendants(folders, childId).Contains(parentId);
    }

    private void Edit(FolderManagerRowViewModel row)
    {
        var vm = new FolderEditorViewModel(row.Folder);
        var window = new FolderEditorWindow { DataContext = vm };
        window.ShowDialog(AppServices.MainWindow);
        Refresh();
    }

    private async void Delete(FolderManagerRowViewModel row)
    {
        var name = row.Folder.Name;
        var confirm = await DialogService.ConfirmAsync(
            AppServices.MainWindow,
            "Delete folder",
            $"Delete \u201C{name}\u201D?\n\nIts subfolders and entries will move one level up - nothing is deleted.",
            "Delete",
            "Cancel",
            danger: true);
        if (!confirm) return;

        AppServices.Database.DeleteFolder(row.Id);
        Refresh();
    }

    [RelayCommand]
    private void Done() => RequestClose?.Invoke();
}