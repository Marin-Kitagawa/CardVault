using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media;
using CardVault.Models;
using CardVault.Services;

namespace CardVault.ViewModels;

public partial class EntryTileViewModel : ViewModelBase
{
    private static readonly Dictionary<EntryKind, StreamGeometry> IconCache = new();

    private readonly Func<EntryTileViewModel, Task>? _onOpen;

    public EntryTileViewModel(VaultEntry entry, Func<EntryTileViewModel, Task>? onOpen = null)
    {
        _onOpen = onOpen;
        Id = entry.Id;
        Kind = entry.Kind;
        Name = entry.Name;
        IsCard = EntryKinds.IsCard(entry.Kind);
        Icon = GetIcon(entry.Kind);

        if (IsCard)
        {
            var brand = Enum.TryParse<CardBrand>(entry.Brand, true, out var b) ? b : CardBrand.Generic;
            var palette = CardBrandInfo.Palette(brand, entry.Accent);
            ColorStart = Color.Parse(palette.start);
            ColorEnd = Color.Parse(palette.end);
            BrandLabel = brand == CardBrand.Generic
                ? string.Empty
                : CardBrandInfo.DisplayName(brand).ToUpperInvariant();
        }
        else
        {
            var (start, end) = entry.Accent >= 0 && entry.Accent < CardBrandInfo.CustomPalettes.Count
                ? CardBrandInfo.CustomPalettes[entry.Accent]
                : (EntryKinds.GraphiteStart, EntryKinds.GraphiteEnd);
            ColorStart = Color.Parse(start);
            ColorEnd = Color.Parse(end);
        }

        OpenCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () =>
        {
            if (_onOpen != null) await _onOpen(this);
        });
    }

    public string Id { get; }
    public EntryKind Kind { get; }
    public string Name { get; }
    public bool IsCard { get; }
    public string KindName => EntryKinds.DisplayName(Kind);
    public StreamGeometry Icon { get; }
    public Color ColorStart { get; }
    public Color ColorEnd { get; }
    public string BrandLabel { get; } = string.Empty;

    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenCommand { get; }

    public static StreamGeometry GetIcon(EntryKind kind)
    {
        if (!IconCache.TryGetValue(kind, out var geometry))
        {
            geometry = StreamGeometry.Parse(EntryKinds.For(kind).IconPath);
            IconCache[kind] = geometry;
        }
        return geometry;
    }
}