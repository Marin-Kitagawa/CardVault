using System;
using Avalonia.Controls;
using CardVault.ViewModels;

namespace CardVault.Views;

public partial class EntryFormWindow : Window
{
    public EntryFormWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is EntryFormViewModel vm)
            vm.RequestClose += Close;
    }
}