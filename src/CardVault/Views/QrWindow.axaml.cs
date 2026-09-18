using Avalonia.Controls;
using CardVault.ViewModels;

namespace CardVault.Views;

public partial class QrWindow : Window
{
    public QrWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is QrViewModel vm)
                vm.RequestClose += () => Close();
        };
    }
}