using System;
using Avalonia.Controls;
using CardVault.ViewModels;

namespace CardVault.Views;

public partial class FolderManagerWindow : Window
{
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
}