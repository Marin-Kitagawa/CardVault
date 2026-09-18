using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CardVault.Models;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

public partial class KindPickerViewModel : ViewModelBase
{
    public event Action<EntryKind>? Picked;
    public event Action? RequestClose;

    public ObservableCollection<KindOptionViewModel> Options { get; } = new();

    public KindPickerViewModel()
    {
        foreach (var info in EntryKinds.All)
            Options.Add(new KindOptionViewModel(info, EntryTileViewModel.GetIcon(info.Kind), Pick));
    }

    private void Pick(EntryKind kind)
    {
        Picked?.Invoke(kind);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}

public partial class KindOptionViewModel : ViewModelBase
{
    private readonly Action<EntryKind> _onPick;

    public KindOptionViewModel(EntryKindInfo info, StreamGeometry icon, Action<EntryKind> onPick)
    {
        Kind = info.Kind;
        Name = info.DisplayName;
        Blurb = info.Blurb;
        Icon = icon;
        _onPick = onPick;
        SelectCommand = new RelayCommand(() => _onPick(Kind));
    }

    public EntryKind Kind { get; }
    public string Name { get; }
    public string Blurb { get; }
    public StreamGeometry Icon { get; }
    public IRelayCommand SelectCommand { get; }
}