using Avalonia.Controls;
using CardVault.Data;
using CardVault.Security;
using CardVault.Services;
using CardVault.ViewModels;

namespace CardVault;

/// <summary>
/// Composition root: wires persistence, crypto session, VMs and services together.
/// </summary>
public static class AppServices
{
    public static VaultSession Session { get; } = new();
    public static VaultDatabase Database { get; private set; } = null!;
    public static ExportService Export { get; } = new();
    public static AutoLockService AutoLock { get; } = new();
    public static AutoBackupService AutoBackup { get; private set; } = null!;
    public static SyncService Sync { get; private set; } = null!;
    public static Window MainWindow { get; set; } = null!;
    public static MainWindowViewModel MainVM { get; private set; } = null!;

    public static void Initialize()
    {
        Database = new VaultDatabase(AppPaths.DatabasePath, Session);
        Database.Open();

        AutoBackup = new AutoBackupService(Database, Session, Export);
        Sync = new SyncService(Database, Session);

        var isNew = !Database.HasMasterKey;
        var setup = new SetupViewModel(isNew);
        var home = new HomeViewModel();
        MainVM = new MainWindowViewModel(setup, home);

        AutoLock.Start(() => Database.LockTimeoutMinutes);
        AutoBackup.Start();
        Sync.Start();
    }
}