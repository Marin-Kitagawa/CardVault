using Avalonia.Controls;

namespace CardVault;

public static class AppPaths
{
    public static string AppDataDir { get; } = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
        "CardVault");

    public static string DatabasePath => DatabasePathOverride ?? System.IO.Path.Combine(AppDataDir, "vault.db");

    /// <summary>Optional override for the vault database location (used by UI tests).</summary>
    public static string? DatabasePathOverride { get; set; }

    public static void EnsureDataDir() => System.IO.Directory.CreateDirectory(AppDataDir);
}