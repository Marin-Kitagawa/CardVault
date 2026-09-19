using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CardVault;
using CardVault.Models;
using CardVault.ViewModels;
using CardVault.Views;
using Xunit;

namespace CardVault.Tests.Ui;

public class HomeRefreshTests
{
    private readonly string _dbPath;

    public HomeRefreshTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"cv-ui-test-{Guid.NewGuid():N}.db");
        AppPaths.DatabasePathOverride = _dbPath;
    }

    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
        Dispatcher.UIThread.RunJobs();
    }

    private static void WaitUntil(Func<bool> done, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (!done())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
    }

    private static T WaitForOwned<T>(Window owner) where T : Window
    {
        T? result = null;
        WaitUntil(() =>
        {
            result = owner.OwnedWindows.OfType<T>().FirstOrDefault(w => w.IsVisible);
            return result is not null;
        });
        return result!;
    }

    private static HomeView HomeViewOf(Window window)
        => window.GetVisualDescendants().OfType<HomeView>().Single();

    private static ItemsControl ItemsControlOf<T>(HomeView view)
    {
        var matches = view.GetVisualDescendants().OfType<ItemsControl>()
            .Where(i => i.ItemCount > 0 && i.ContainerFromIndex(0)?.DataContext is T).ToList();
        Assert.Single(matches);
        return matches[0];
    }

    private static void AssertAllRealized(ItemsControl items, string what)
    {
        for (var i = 0; i < items.ItemCount; i++)
            Assert.NotNull(items.ContainerFromIndex(i));
    }

    private static MainWindow ShowMainWindow()
    {
        var window = new MainWindow { DataContext = AppServices.MainVM };
        AppServices.MainWindow = window;
        window.Show();
        Pump();
        return window;
    }

    [AvaloniaFact]
    public void Refresh_OnUiThread_AfterAddingFolderAndEntry_RealizesContainers()
    {
        AppServices.Initialize();
        AppServices.Database.CreateVault("password");

        var window = ShowMainWindow();

        AppServices.Database.CreateFolder("Banking", "wallet");
        AppServices.Database.CreateFolder("Travel", "plane");
        var entry = AppServices.Database.CreateEntry("WiFi", EntryKind.Login, 0, new EntrySecureData { Notes = "x" });
        AppServices.Database.InsertEntry(entry);

        AppServices.MainVM.Home.Refresh();
        Pump();

        Assert.Equal(2, AppServices.MainVM.Home.FolderTree.Count);
        Assert.Single(AppServices.MainVM.Home.Entries);

        var home = HomeViewOf(window);
        AssertAllRealized(ItemsControlOf<FolderNodeViewModel>(home), "folder tree");
        AssertAllRealized(ItemsControlOf<EntryTileViewModel>(home), "entries grid");
    }

    [AvaloniaFact]
    public void UnlockOnBackgroundThread_RefreshIsMarshaledToUiThread()
    {
        AppServices.Initialize();
        AppServices.Database.CreateVault("password");
        AppServices.Database.CreateFolder("Banking", "wallet");
        var entry = AppServices.Database.CreateEntry("WiFi", EntryKind.Login, 0, new EntrySecureData { Notes = "x" });
        AppServices.Database.InsertEntry(entry);

        var window = ShowMainWindow();
        AppServices.MainVM.Home.Refresh();
        Pump();

        // Any mutation of Home's bound collections must happen on the UI thread.
        AppServices.MainVM.Home.Entries.CollectionChanged += (_, _) =>
        {
            if (!Dispatcher.UIThread.CheckAccess())
                throw new InvalidOperationException("Entries was mutated off the UI thread during unlock.");
        };
        AppServices.MainVM.Home.FolderTree.CollectionChanged += (_, _) =>
        {
            if (!Dispatcher.UIThread.CheckAccess())
                throw new InvalidOperationException("FolderTree was mutated off the UI thread during unlock.");
        };

        AppServices.Session.Lock();
        Pump();

        // Mirrors SetupViewModel.SubmitAsync: the PBKDF2 unlock (and therefore
        // Session.Open -> Unlocked -> MainWindowViewModel.OnUnlocked -> Home.Refresh)
        // runs on a background thread and must be marshaled back onto the UI thread.
        using var done = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try { AppServices.Database.TryUnlock("password"); }
            finally { done.Set(); }
        })
        { IsBackground = true };
        thread.Start();
        done.Wait();
        Pump();

        Assert.True(AppServices.Session.IsUnlocked);
        Assert.Single(AppServices.MainVM.Home.FolderTree);
        Assert.Single(AppServices.MainVM.Home.Entries);

        var home = HomeViewOf(window);
        AssertAllRealized(ItemsControlOf<FolderNodeViewModel>(home), "folder tree");
        AssertAllRealized(ItemsControlOf<EntryTileViewModel>(home), "entries grid");
    }

    [AvaloniaFact]
    public void QuickCreateFolder_DialogFlow_ShowsNewFolderInTreeImmediately()
    {
        AppServices.Initialize();
        AppServices.Database.CreateVault("password");

        var window = ShowMainWindow();

        // Drive the real quick-create flow: sidebar "+" opens a FolderEditorWindow.
        // In the headless session the sync ShowDialog returns with the dialog still
        // open, so drive it deterministically, then replicate the post-dialog refresh.
        AppServices.MainVM.Home.AddFolderCommand.Execute(null);
        Pump();

        var editor = window.OwnedWindows.OfType<FolderEditorWindow>().FirstOrDefault(w => w.IsVisible);
        Assert.NotNull(editor);
        var vm = (FolderEditorViewModel)editor!.DataContext!;
        vm.FolderName = "NewFolder";
        vm.SaveCommand.Execute(null);
        Pump();

        Assert.Single(AppServices.Database.ListFolders());

        AppServices.MainVM.Home.Refresh();
        Pump();

        var list = AppServices.MainVM.Home.FolderTree;
        Assert.Single(list);
        Assert.Equal("NewFolder", list[0].Name);
        AssertAllRealized(ItemsControlOf<FolderNodeViewModel>(HomeViewOf(window)), "folder tree");
    }

    [AvaloniaFact]
    public void AddEntry_DialogFlow_ShowsNewAndExistingEntriesImmediately()
    {
        AppServices.Initialize();
        AppServices.Database.CreateVault("password");

        var window = ShowMainWindow();

        // An entry saved earlier (from a previous session) - not yet refreshed into the UI.
        var existing = AppServices.Database.CreateEntry("Existing", EntryKind.Login, 0, new EntrySecureData { Notes = "x" });
        AppServices.Database.InsertEntry(existing);
        AppServices.MainVM.Home.Refresh();
        Pump();
        Assert.Single(AppServices.MainVM.Home.Entries);

        // Drive the real add flow: kind picker -> entry form -> save. AddEntryCommand
        // is async and the awaited ShowDialog keeps pumping the dispatcher, so the
        // posted job can orchestrate both dialogs from inside the modal loop.
        Dispatcher.UIThread.Post(() =>
        {
            var picker = WaitForOwned<KindPickerWindow>(window);
            var pvm = (KindPickerViewModel)picker.DataContext!;
            pvm.Options.First(o => o.Kind == EntryKind.Login).SelectCommand.Execute(null);

            var form = WaitForOwned<EntryFormWindow>(window);
            var fvm = (EntryFormViewModel)form.DataContext!;
            fvm.CardName = "NewItem";
            fvm.SaveCommand.Execute(null);
        });

        AppServices.MainVM.Home.AddEntryCommand.Execute(null);
        WaitUntil(() => AppServices.MainVM.Home.Entries.Count == 2);

        Assert.Contains(AppServices.MainVM.Home.Entries, e => e.Name == "Existing");
        Assert.Contains(AppServices.MainVM.Home.Entries, e => e.Name == "NewItem");
        AssertAllRealized(ItemsControlOf<EntryTileViewModel>(HomeViewOf(window)), "entries grid");
    }
}