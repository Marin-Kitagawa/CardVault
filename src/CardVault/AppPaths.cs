using Avalonia.Controls;

namespace CardVault;

public static class AppPaths
{
    public static string AppDataDir { get; } = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "CardVault");

    public static string DatabasePath => System.IO.Path.Combine(AppDataDir, "vault.db");

    public static void EnsureDataDir() => System.IO.Directory.CreateDirectory(AppDataDir);
}