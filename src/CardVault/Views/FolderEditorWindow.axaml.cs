using System;
using Avalonia.Controls;
using CardVault.ViewModels;

namespace CardVault.Views;

public partial class FolderEditorWindow : Window
{
    public FolderEditorWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is FolderEditorViewModel vm)
            vm.RequestClose += Close;
    }
}