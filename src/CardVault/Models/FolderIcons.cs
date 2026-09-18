using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace CardVault.Models;

/// <summary>One selectable folder icon.</summary>
public sealed record FolderIconInfo(string Key, string Name, string Path, string Category);

/// <summary>
/// Folder icon library: a curated set of Lucide-style stroke glyphs (24-unit
/// view-box, rendered via StreamGeometry, see Assets/Icons/LICENSES.Lucide.txt).
/// Circles are emitted as twin arcs so every glyph renders with a single stroked
/// Path. <see cref="All"/> drives the icon picker grid; folders persist only the
/// <see cref="FolderIconInfo.Key"/>.
/// </summary>
public static class FolderIcons
{
    private const string FolderBase =
        "M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2z";

    private const string FolderOpen =
        "m6 14 1.45-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.55 6a2 2 0 0 1-1.94 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2";

    public static IReadOnlyList<FolderIconInfo> All { get; } = new List<FolderIconInfo>
    {
        new("folder", "Folder", FolderBase, "Folders"),
        new("folder-open", "Folder open", FolderOpen, "Folders"),
        new("folder-plus", "Folder plus", FolderBase + " M12 10v7 M9 13.5h6", "Folders"),
        new("folder-minus", "Folder minus", FolderBase + " M9 13.5h6", "Folders"),
        new("star", "Star", "M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.12 2.12 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.12 2.12 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.12 2.12 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.12 2.12 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.12 2.12 0 0 0 1.597-1.16z", "Essentials"),
        new("heart", "Heart", "M19 14c1.49-1.46 3-3.21 3-5.5A5.5 5.5 0 0 0 16.5 3c-1.76 0-3 .5-4.5 2-1.5-1.5-2.74-2-4.5-2A5.5 5.5 0 0 0 2 8.5c0 2.3 1.5 4.05 3 5.5l7 7z", "Essentials"),
        new("user", "Person", "M12 3.5a4 4 0 1 0 0 8 4 4 0 0 0 0-8 M5 21c0-3.9 3.1-6 7-6s7 2.1 7 6", "People"),
        new("users", "People", "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M8 7a4 4 0 1 0 8 0 4 4 0 0 0-8 0 M22 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75", "People"),
        new("key", "Password", IconPaths.Key, "Security"),
        new("lock", "Lock", IconPaths.Lock, "Security"),
        new("shield", "Shield", "M12 22s8-3.5 8-10V5l-8-3-8 3v7c0 6.5 8 10 8 10z", "Security"),
        new("shield-check", "Protected", "M12 22s8-3.5 8-10V5l-8-3-8 3v7c0 6.5 8 10 8 10z M8.5 11.5l2.5 2.5 4.5-4.5", "Security"),
        new("credit-card", "Card", IconPaths.CreditCard, "Finance"),
        new("wallet", "Wallet", "M3 7h16a2 2 0 0 1 2 2v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z M16 3H6a3 3 0 0 0-3 3v13 M16 12h.01", "Finance"),
        new("banknote", "Money", "M2 7a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6 M6 9h.01 M18 15h.01", "Finance"),
        new("coins", "Coins", "M12 3a8 8 0 1 0 0 16 8 8 0 0 0 0-16 M4.5 8.1a8 8 0 0 0 11.4 11.4", "Finance"),
        new("landmark", "Bank", "M3 22h18 M6 18h12 M3 14l9-5 9 5 M4 14l1-8h14l1 8 M8 11v3 M12 11v3 M16 11v3", "Finance"),
        new("account", "Institution", IconPaths.Account, "Finance"),
        new("crypto", "Crypto", IconPaths.Crypto, "Finance"),
        new("home", "Home", "M3 11.5 12 4l9 7.5 M5 10v10a1 1 0 0 0 1 1h4v-6h4v6h4a1 1 0 0 0 1-1V10", "Home"),
        new("building", "Building", "M6 22V4a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v18 M4 22h16 M10 6h.01 M14 6h.01 M10 10h.01 M14 10h.01 M10 14h.01 M14 14h.01", "Home"),
        new("briefcase", "Work", "M4 9h16v10a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1z M6 9V5h12v4 M12 12v.01", "Home"),
        new("briefcase-lock", "Work secure", "M4 9h16v10a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1z M6 9V5h12v4 M10 14h4 M12 12.5v1.5 M12 16v1", "Home"),
        new("shopping-bag", "Shopping", "M6 7h12l1 14H5z M9 7a3 3 0 0 1 6 0", "Shopping"),
        new("gift", "Gift", IconPaths.Gift, "Shopping"),
        new("tag", "Tag", "M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z M7 7h.01", "Shopping"),
        new("plane", "Travel", "M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z", "Travel"),
        new("car", "Car", "M5 11 7 5h10l2 6 M5 11h14l1.5 4.5H3.5z M7 15.5h.01 M17 15.5h.01 M9 19a2 2 0 1 1-4 0 2 2 0 0 1 4 0z M19 19a2 2 0 1 1-4 0 2 2 0 0 1 4 0z", "Travel"),
        new("map-pin", "Location", "M12 21s-7-5.5-7-11a7 7 0 0 1 14 0c0 5.5-7 11-7 11z M12 10a3 3 0 1 0 0 6 3 3 0 0 0 0-6", "Travel"),
        new("compass", "Compass", "M13 4a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M15.5 8.5l-2 5-5 2 2-5z", "Travel"),
        new("camera", "Camera", "M2 8a2 2 0 0 1 2-2h2l2-2h4l2 2h4a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2z M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6", "Media"),
        new("music", "Music", "M9 18V5l12-2v13 M9 15a3 3 0 1 0 0 6 3 3 0 0 0 0-6 M21 13a3 3 0 1 0 0 6 3 3 0 0 0 0-6", "Media"),
        new("gamepad", "Gaming", "M6 11h4 M8 9v4 M15 12h.01 M18 10h.01 M17.3 5H6.7A4.7 4.7 0 0 0 2 9.7 4 4 0 0 0 4.5 13l1.5 2.5V19h5v-3h2v3h5v-3.5L19.5 13A4 4 0 0 0 22 9.7 4.7 4.7 0 0 0 17.3 5z", "Media"),
        new("book", "Reading", "M4 19.5A2.5 2.5 0 0 1 6.5 17H20 M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z", "Media"),
        new("document", "Document", IconPaths.Document, "Productivity"),
        new("note", "Note", IconPaths.Note, "Productivity"),
        new("pen", "Edit", "M12 20h9 M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4z", "Productivity"),
        new("calendar", "Dates", "M3 5h18v16a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z M3 9h18 M8 3v6 M16 3v6 M8 14h.01 M16 14h.01", "Productivity"),
        new("clock", "Time", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M12 7v5l3 3", "Productivity"),
        new("mail", "Mail", "M4 5h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z M3 7l9 6 9-6", "Communication"),
        new("phone", "Phone", "M5 3h3.5l1.5 4L8 8.5a13 13 0 0 0 7.5 7.5L18 14l4 1.5V19a2 2 0 0 1-2 2A16 16 0 0 1 3 5a2 2 0 0 1 2-2z", "Communication"),
        new("smartphone", "Smartphone", "M6 2h12a1 1 0 0 1 1 1v18a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z M12 18h.01", "Communication"),
        new("globe", "Globe", "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18 M3 12h18 M12 3c2.5 2.6 4 5.5 4 9s-1.5 6.4-4 9c-2.5-2.6-4-5.5-4-9s1.5-6.4 4-9z", "Communication"),
        new("wifi", "Wi-Fi", IconPaths.Wifi, "Technology"),
        new("cloud", "Cloud", "M6 16.5a4 4 0 0 1-.5-7.96A6 6 0 0 1 17.4 8.7 4.5 4.5 0 0 1 17.5 16.5z M6 16.5H17.5", "Technology"),
        new("laptop", "Computer", "M4 5h16v11H4z M2 20h20 M10 15h4", "Technology"),
        new("monitor", "Monitor", "M4 4h16a1 1 0 0 1 1 1v11a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V5a1 1 0 0 1 1-1z M9 20h6 M12 16v4", "Technology"),
        new("cpu", "Chip", "M4 4h16v16H4z M9 9h6v6H9z M9 2v2 M15 2v2 M9 20v2 M15 20v2 M2 9h2 M2 15h2 M20 9h2 M20 15h2", "Technology"),
        new("server", "Server", "M3 5h18a1 1 0 0 1 1 1v4a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1z M3 13h18a1 1 0 0 1 1 1v4a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1v-4a1 1 0 0 1 1-1z M7 8h.01 M7 16h.01 M11 8h.01 M11 16h.01", "Technology"),
        new("database", "Database", "M12 3c4.4 0 8 1.3 8 3s-3.6 3-8 3-8-1.3-8-3 3.6-3 8-3z M4 6v6c0 1.7 3.6 3 8 3s8-1.3 8-3V6 M4 12v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6", "Technology"),
        new("hard-drive", "Storage", "M3 8V6a1 1 0 0 1 1-1h16a1 1 0 0 1 1 1v2 M3 8h18l2 6v4a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1v-4z M10 15h.01 M14 15h.01", "Technology"),
        new("search", "Search", "M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14 M21 21l-4.35-4.35", "Productivity"),
        new("sliders", "Settings", "M4 21v-7 M4 10V3 M12 21v-9 M12 8V3 M20 21v-5 M20 12V3 M1 14h6 M9 8h6 M17 16h6", "Productivity"),
        new("dumbbell", "Fitness", "M6 6v12 M18 6v12 M3 9v6 M21 9v6 M6 9h3 M15 9h3 M6 15h3 M15 15h3", "Lifestyle"),
        new("leaf", "Nature", "M4 20c0-9 8-16 16-16 0 9-8 16-16 16z M4 20c4-6 8-10 12-12", "Lifestyle"),
        new("sprout", "Growth", "M12 22v-8 M12 14c-4 0-6-2-6-6 4 0 6 2 6 6z M12 14c0-4 2-6 6-6 0 4-2 6-6 6z", "Lifestyle"),
    };

    public static FolderIconInfo Default => All[0];

    public static FolderIconInfo Find(string? key)
        => string.IsNullOrEmpty(key) ? Default : All.FirstOrDefault(i => i.Key == key) ?? Default;

    private static readonly Dictionary<string, StreamGeometry> GeometryCache = new();

    public static StreamGeometry GetGeometry(string? key)
    {
        var resolved = Find(key);
        if (GeometryCache.TryGetValue(resolved.Key, out var cached)) return cached;
        try
        {
            cached = StreamGeometry.Parse(resolved.Path)
                ?? StreamGeometry.Parse(Default.Path);
        }
        catch
        {
            cached = StreamGeometry.Parse(Default.Path);
        }
        if (cached is null) return cached!; // unreachable
        GeometryCache[resolved.Key] = cached;
        return cached;
    }

    public static IReadOnlyList<string> Categories { get; } =
        All.Select(i => i.Category).Distinct().ToList();
}