using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CardVault.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty] private SetupViewModel setup;
    [ObservableProperty] private HomeViewModel home;

    [ObservableProperty]
    private bool isLocked = true;

    public MainWindowViewModel(SetupViewModel setup, HomeViewModel home)
    {
        Setup = setup;
        Home = home;

        AppServices.Session.Locked += OnLocked;
        AppServices.Session.Unlocked += OnUnlocked;
    }

    public bool IsUnlocked => !IsLocked;

    private void OnLocked()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnLocked);
            return;
        }

        IsLocked = true;
        OnPropertyChanged(nameof(IsUnlocked));
        Home.Refresh();
    }

    private void OnUnlocked()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(OnUnlocked);
            return;
        }

        IsLocked = false;
        OnPropertyChanged(nameof(IsUnlocked));
        Home.Refresh();
    }
}