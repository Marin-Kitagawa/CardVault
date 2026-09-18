using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CardVault.Services;
using CardVault.ViewModels;
using CardVault.Views;

namespace CardVault;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AppServices.Initialize();
            ThemeService.ApplySaved(AppServices.Database);

            var window = new MainWindow { DataContext = AppServices.MainVM };
            AppServices.MainWindow = window;
            desktop.MainWindow = window;

            window.PointerMoved += (_, _) => AppServices.AutoLock.Touch();
            window.PointerPressed += (_, _) => AppServices.AutoLock.Touch();
            window.KeyDown += (_, _) => AppServices.AutoLock.Touch();

            desktop.ShutdownRequested += (_, _) => AppServices.Session.Lock();
        }

        base.OnFrameworkInitializationCompleted();
    }
}