using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using CardVault.ViewModels;

namespace CardVault.Views;

public partial class FolderManagerWindow : Window
{
    private const string FolderIdFormat = "cardvault/folder-id";
    private const double DragThreshold = 6;

    private Point _dragStart;
    private bool _dragArmed;

    public FolderManagerWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is FolderManagerViewModel vm)
            vm.RequestClose += Close;
    }

    private FolderManagerViewModel? Vm => DataContext as FolderManagerViewModel;

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border row) return;
        _dragArmed = false;
        if (!e.GetCurrentPoint(row).Properties.IsLeftButtonPressed || ComesFromButton(e.Source, row)) return;

        _dragStart = e.GetPosition(row);
        _dragArmed = true;
    }

    private void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragArmed || sender is not Border row || row.DataContext is not FolderManagerRowViewModel rowVm) return;
        var pos = e.GetPosition(row);
        if (Math.Abs(pos.X - _dragStart.X) + Math.Abs(pos.Y - _dragStart.Y) < DragThreshold) return;

        _dragArmed = false;
        var data = new DataObject();
        data.Set(FolderIdFormat, rowVm.Id);
        DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
    }

    private static bool ComesFromButton(object? source, Border row)
    {
        var el = source as Control;
        while (el is not null && el != row)
        {
            if (el is Button) return true;
            el = el.Parent as Control;
        }

        return false;
    }

    private void OnRowDragOver(object? sender, DragEventArgs e)
    {
        var vm = Vm;
        if (vm is null || sender is not Border { DataContext: FolderManagerRowViewModel target } ||
            e.Data.Get(FolderIdFormat) is not string childId)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        var folders = vm.Rows.Select(r => r.Folder).ToList();
        e.DragEffects = FolderManagerViewModel.CanAssign(folders, childId, target.Id)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnRowDrop(object? sender, DragEventArgs e)
    {
        var vm = Vm;
        if (vm is null || e.Data.Get(FolderIdFormat) is not string childId ||
            sender is not Border { DataContext: FolderManagerRowViewModel target }) return;

        vm.AssignParent(childId, target.Id);
        e.Handled = true;
    }

    private void OnRootDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Get(FolderIdFormat) is string ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void OnRootDrop(object? sender, DragEventArgs e)
    {
        var vm = Vm;
        if (vm is null || e.Data.Get(FolderIdFormat) is not string childId) return;

        vm.AssignParent(childId, string.Empty);
        e.Handled = true;
    }
}