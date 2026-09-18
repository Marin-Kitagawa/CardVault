using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using CardVault.Data;

namespace CardVault.Services;

public enum ThemeKind
{
    Atelier,
    Readout,
}

/// <summary>
/// Loads one of the world palettes (Atelier / Readout) into the application
/// resources. Every themed value is consumed through DynamicResource, so an
/// Apply() call re-skins every open window instantly.
/// </summary>
public static class ThemeService
{
    public const string MetaKey = "theme";

    private static readonly IReadOnlyDictionary<ThemeKind, string> Sources =
        new Dictionary<ThemeKind, string>
        {
            [ThemeKind.Atelier] = "avares://CardVault/Styles/Themes/AtelierTheme.axaml",
            [ThemeKind.Readout] = "avares://CardVault/Styles/Themes/ReadoutTheme.axaml",
        };

    private static readonly HashSet<string> FontKeys = new()
    {
        "FontBody", "FontDisplay", "FontMono", "FontDigits",
    };

    public static ThemeKind Current { get; private set; } = ThemeKind.Atelier;

    public static void ApplySaved(VaultDatabase? db)
    {
        var kind = ThemeKind.Atelier;
        if (db is not null
            && Enum.TryParse<ThemeKind>(db.Theme, ignoreCase: true, out var saved))
        {
            kind = saved;
        }

        Apply(kind, db);
    }

    public static void Apply(ThemeKind kind, VaultDatabase? db = null)
    {
        Current = kind;

        var app = Application.Current;
        if (app is null) return;

        app.RequestedThemeVariant = kind == ThemeKind.Readout
            ? ThemeVariant.Dark
            : ThemeVariant.Light;

        var palette = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(Sources[kind]));
        var resources = app.Resources;

        foreach (var pair in palette)
        {
            var key = (string)pair.Key;
            var value = pair.Value;
            if (value is string text && FontKeys.Contains(key))
                value = new FontFamily(text);
            resources[key] = value;
        }

        if (db is not null) db.Theme = kind.ToString();
    }
}