using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CardVault.Models;
using CardVault.Services;
using CardVault.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

public partial class EntryDetailsViewModel : ViewModelBase
{
    private readonly VaultEntry _entry;
    private readonly CardSecureData? _card;
    private readonly EntrySecureData? _generic;

    public event Action? RequestClose;

    [ObservableProperty] private bool revealed;

    public EntryDetailsViewModel(VaultEntry entry, object payload)
    {
        _entry = entry;
        IsCard = EntryKinds.IsCard(entry.Kind);

        if (payload is CardSecureData card)
        {
            _card = card;
            var brand = Enum.TryParse<CardBrand>(entry.Brand, true, out var b) ? b : CardBrand.Generic;
            var palette = CardBrandInfo.Palette(brand, entry.Accent);
            ColorStart = Color.Parse(palette.start);
            ColorEnd = Color.Parse(palette.end);
            BrandLabel = brand == CardBrand.Generic
                ? string.Empty
                : CardBrandInfo.DisplayName(brand).ToUpperInvariant();

            foreach (var s in (card.Secrets ?? new List<SecretEntry>())
                         .Where(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Value)))
                SecretRows.Add(new SecretRowViewModel(s.Name.Trim(), s.Value));
        }
        else if (payload is EntrySecureData generic)
        {
            _generic = generic;
            foreach (var def in EntryKinds.For(entry.Kind).Fields)
            {
                var value = generic.Fields.FirstOrDefault(f => f.Label == def.Label)?.Value ?? string.Empty;
                if (value.Length > 0)
                    Rows.Add(new SecretRowViewModel(def.Label, value));
            }

            foreach (var s in (generic.Secrets ?? new List<SecretEntry>())
                         .Where(x => !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Value)))
                Rows.Add(new SecretRowViewModel(s.Name.Trim(), s.Value));
        }
    }

    public string Name => _entry.Name;
    public bool IsCard { get; }
    public string KindName => EntryKinds.DisplayName(_entry.Kind);
    public string HeaderSubtitle => IsCard ? "Card details" : $"{KindName} details";
    public StreamGeometry Icon => EntryTileViewModel.GetIcon(_entry.Kind);

    public string BrandLabel { get; } = string.Empty;
    public Color ColorStart { get; } = Color.Parse(EntryKinds.GraphiteStart);
    public Color ColorEnd { get; } = Color.Parse(EntryKinds.GraphiteEnd);

    // ---------- card kind ----------

    public string RevealLabel => Revealed ? "Hide details" : "Show details";
    public string RevealStatus => Revealed
        ? "Sensitive details are visible on screen."
        : "Details are masked. Reveal them when you need them.";

    public string NumberShown => Revealed ? CardFormat.Number(_card?.Number ?? string.Empty) : CardFormat.MaskedNumber(_card?.Number ?? string.Empty);
    public string HolderShown => string.IsNullOrEmpty(_card?.Holder) ? "—" : Revealed ? _card.Holder : CardFormat.Masked(_card.Holder);
    public string CvvShown => string.IsNullOrEmpty(_card?.Cvv) ? "—" : Revealed ? _card.Cvv : CardFormat.Masked(_card.Cvv);
    public string ExpiryShown => _card is not null && _card.ExpiryMonth.Length > 0
        ? $"{_card.ExpiryMonth}/{_card.ExpiryYear}"
        : "—";

    public List<SecretRowViewModel> SecretRows { get; } = new();
    public bool HasSecrets => SecretRows.Count > 0;

    // ---------- generic kind ----------

    public List<SecretRowViewModel> Rows { get; } = new();
    public bool HasRows => Rows.Count > 0;

    public string NotesDisplay
    {
        get
        {
            var notes = IsCard ? _card?.Notes : _generic?.Notes;
            return string.IsNullOrWhiteSpace(notes) ? "No notes for this entry." : notes;
        }
    }

    partial void OnRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(NumberShown));
        OnPropertyChanged(nameof(HolderShown));
        OnPropertyChanged(nameof(CvvShown));
        OnPropertyChanged(nameof(RevealLabel));
        OnPropertyChanged(nameof(RevealStatus));
        CopyNumberCommand.NotifyCanExecuteChanged();
        CopyHolderCommand.NotifyCanExecuteChanged();
        CopyCvvCommand.NotifyCanExecuteChanged();
    }

    private bool CanCopy => Revealed;

    [RelayCommand]
    private void ToggleReveal() => Revealed = !Revealed;

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private System.Threading.Tasks.Task CopyNumber() => CopyAsync(CardFormat.Number(_card?.Number ?? string.Empty));

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private System.Threading.Tasks.Task CopyHolder() => CopyAsync(_card?.Holder ?? string.Empty);

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private System.Threading.Tasks.Task CopyCvv() => CopyAsync(_card?.Cvv ?? string.Empty);

    internal static System.Threading.Tasks.Task CopyAsync(string text)
    {
        var clipboard = AppServices.MainWindow?.Clipboard;
        if (clipboard is null || string.IsNullOrEmpty(text)) return System.Threading.Tasks.Task.CompletedTask;
        return clipboard.SetTextAsync(text);
    }

    [RelayCommand]
    private void Done() => RequestClose?.Invoke();

    [RelayCommand]
    private void ShareQr()
    {
        var options = new List<QrValueOption> { new("Entry name", _entry.Name) };
        if (IsCard)
        {
            if (Revealed && _card is not null)
            {
                if (!string.IsNullOrWhiteSpace(_card.Holder)) options.Add(new QrValueOption("Cardholder name", _card.Holder));
                if (!string.IsNullOrWhiteSpace(_card.Number)) options.Add(new QrValueOption("Card number", _card.Number));
                if (!string.IsNullOrWhiteSpace(_card.Cvv)) options.Add(new QrValueOption("Security code", _card.Cvv));
            }
        }
        else
        {
            foreach (var row in Rows.Where(r => r.Revealed))
                options.Add(new QrValueOption(row.Name, row.Value));
        }

        var vm = new QrViewModel(options);
        var window = new QrWindow { DataContext = vm };
        window.ShowDialog(AppServices.MainWindow);
    }

    [RelayCommand]
    private void Edit()
    {
        RequestClose?.Invoke();
        var vm = new EntryFormViewModel(_entry, _entry.Kind, _card, _generic);
        var window = new EntryFormWindow { DataContext = vm };
        window.ShowDialog(AppServices.MainWindow);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task DeleteAsync()
    {
        var confirm = await DialogService.ConfirmAsync(
            AppServices.MainWindow,
            "Delete entry",
            $"Remove \u201C{_entry.Name}\u201D from your vault?\n\nThis cannot be undone.",
            "Delete",
            "Cancel",
            danger: true);
        if (!confirm) return;

        AppServices.Database.DeleteEntry(_entry.Id);
        RequestClose?.Invoke();
    }
}

/// <summary>
/// One stored secret value. Revealed individually and only then copyable,
/// matching the vault's "show what you asked for" discipline.
/// </summary>
public partial class SecretRowViewModel : ViewModelBase
{
    private const string Mask = "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022";

    private readonly string _value;

    [ObservableProperty] private bool revealed;

    public SecretRowViewModel(string name, string value)
    {
        Name = name;
        _value = value;
    }

    public string Name { get; }

    public string Value => _value;

    public string ValueShown => Revealed ? _value : Mask;
    public string RevealLabel => Revealed ? "Hide" : "Reveal";
    public bool IsMasked => !Revealed;

    partial void OnRevealedChanged(bool value)
    {
        OnPropertyChanged(nameof(ValueShown));
        OnPropertyChanged(nameof(RevealLabel));
        OnPropertyChanged(nameof(IsMasked));
        RevealCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
    }

    private bool CanCopyValue => Revealed;

    [RelayCommand]
    private void Reveal() => Revealed = !Revealed;

    [RelayCommand(CanExecute = nameof(CanCopyValue))]
    private System.Threading.Tasks.Task Copy() => EntryDetailsViewModel.CopyAsync(_value);
}