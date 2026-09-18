using System;
using Avalonia.Controls;
using CardVault.ViewModels;

namespace CardVault.Views;

public partial class EntryDetailsWindow : Window
{
    public EntryDetailsWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is EntryDetailsViewModel vm)
            vm.RequestClose += Close;
    }
}