using System;
using Avalonia.Controls;
using CardVault.ViewModels;

namespace CardVault.Views;

public partial class KindPickerWindow : Window
{
    public KindPickerWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is KindPickerViewModel vm)
            vm.RequestClose += Close;
    }
}