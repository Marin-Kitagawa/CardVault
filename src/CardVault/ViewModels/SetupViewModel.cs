using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

public partial class SetupViewModel : ViewModelBase
{
    private readonly bool _isNewVault;
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _showColon = true;

    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string confirm = string.Empty;
    [ObservableProperty] private string? error;
    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private string clockText = DateTime.Now.ToString("HH:mm");

    public SetupViewModel(bool isNewVault)
    {
        _isNewVault = isNewVault;
        _clockTimer.Tick += (_, _) =>
        {
            _showColon = !_showColon;
            ClockText = _showColon ? DateTime.Now.ToString("HH:mm") : DateTime.Now.ToString("HH mm");
        };
        _clockTimer.Start();
    }

    public bool IsNewVault => _isNewVault;
    public bool HasError => Error is not null;
    public bool IsIdle => !IsBusy;
    public string Title => _isNewVault ? "Create your vault" : "Welcome back";
    public string Subtitle => _isNewVault
        ? "Choose a strong master password. It encrypts every card stored on this device."
        : "Enter your master password to unlock your cards.";
    public string SubmitLabel => _isNewVault ? "Create vault" : "Unlock";
    public bool ShowConfirm => _isNewVault;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;

        if (Password.Length < 8)
        {
            Error = "Your master password needs at least 8 characters.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        if (_isNewVault && Password != Confirm)
        {
            Error = "The two passwords do not match.";
            OnPropertyChanged(nameof(HasError));
            return;
        }

        IsBusy = true;
        Error = null;
        OnPropertyChanged(nameof(HasError));

        try
        {
            var ok = await Task.Run(() =>
            {
                if (_isNewVault) { AppServices.Database.CreateVault(Password); return true; }
                return AppServices.Database.TryUnlock(Password);
            });

            if (!ok)
            {
                Error = "Incorrect password. Try again.";
                OnPropertyChanged(nameof(HasError));
            }
            else
            {
                AppServices.Sync.OnUnlocked(Password);
            }
        }
        catch (Exception ex)
        {
            Error = "Could not open the vault: " + ex.Message;
            OnPropertyChanged(nameof(HasError));
        }
        finally
        {
            Password = string.Empty;
            Confirm = string.Empty;
            IsBusy = false;
        }
    }
}