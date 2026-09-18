using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia.Media;
using CardVault.Models;
using CardVault.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CardVault.ViewModels;

public partial class EntryFormViewModel : ViewModelBase
{
    private static readonly Regex HolderNameRegex = new(@"^[A-Za-zÀ-ÖØ-öø-ÿ .'\-]+$", RegexOptions.Compiled);
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#2FD47C"));
    private static readonly IBrush DangerBrush = new SolidColorBrush(Color.Parse("#FF5A71"));
    private static readonly IBrush NeutralBrush = new SolidColorBrush(Color.Parse("#687081"));

    private readonly VaultEntry? _existing;
    private readonly CardSecureData? _cardData;
    private readonly EntrySecureData? _genericData;
    private bool _updating;
    private string _brandLabel = string.Empty;

    public event Action? RequestClose;

    [ObservableProperty] private string cardName = string.Empty;
    [ObservableProperty] private string holder = string.Empty;
    [ObservableProperty] private string number = string.Empty;
    [ObservableProperty] private string expiry = string.Empty;
    [ObservableProperty] private string cvv = string.Empty;
    [ObservableProperty] private string notes = string.Empty;
    [ObservableProperty] private string tagsText = string.Empty;
    [ObservableProperty] private int selectedSwatch;
    [ObservableProperty] private FolderOptionViewModel? selectedFolder;

    [ObservableProperty] private string numberHint = "Enter the digits printed on the card";
    [ObservableProperty] private bool numberOkay;
    [ObservableProperty] private string expiryHint = "MM / YY";
    [ObservableProperty] private bool expiryOkay;
    [ObservableProperty] private string cvvHint = "Security code";
    [ObservableProperty] private bool cvvOkay;
    [ObservableProperty] private string holderHint = "The name embossed on the card";
    [ObservableProperty] private bool holderOkay;

    public const int MaxSecretEntries = 12;
    public const int MaxFieldLength = 200;

    public ObservableCollection<SwatchViewModel> Swatches { get; } = new();
    public ObservableCollection<EntryFieldRowViewModel> TemplateFields { get; } = new();
    public ObservableCollection<SecretEntryViewModel> SecretEntries { get; } = new();
    public ObservableCollection<FolderOptionViewModel> Folders { get; } = new();

    public EntryKind Kind { get; }
    public bool IsCard => EntryKinds.IsCard(Kind);
    public string KindName => EntryKinds.DisplayName(Kind);
    public bool HasTemplateFields => TemplateFields.Count > 0;

    public string WindowTitle => _existing is null
        ? (IsCard ? "Add card" : $"Add {KindName}")
        : (IsCard ? "Edit card" : $"Edit {KindName}");
    public string SaveLabel => _existing is null
        ? (IsCard ? "Add card" : "Add")
        : "Save changes";
    public string BrandLabel => _brandLabel;
    public StreamGeometry Icon => EntryTileViewModel.GetIcon(Kind);
    public Color PreviewColorStart { get; private set; } = Color.Parse("#38342D");
    public Color PreviewColorEnd { get; private set; } = Color.Parse("#1A1813");
    public bool CanSave { get; private set; }

    public IBrush NumberHintBrush => HintBrush(NumberOkay, Number.Length > 0);
    public IBrush ExpiryHintBrush => HintBrush(ExpiryOkay, ExpiryDigits.Length >= 4);
    public IBrush CvvHintBrush => HintBrush(CvvOkay, Cvv.Length > 0);
    public IBrush HolderHintBrush => HintBrush(HolderOkay, Holder.Length > 0);

    private static IBrush HintBrush(bool okay, bool hasValue)
        => hasValue ? (okay ? SuccessBrush : DangerBrush) : NeutralBrush;

    public EntryFormViewModel(VaultEntry? existing, EntryKind kind,
        CardSecureData? card = null, EntrySecureData? generic = null)
    {
        _existing = existing;
        _cardData = card;
        _genericData = generic;
        Kind = kind;

        _updating = true;
        if (existing is not null && card is not null && IsCard)
        {
            CardName = existing.Name;
            Holder = card.Holder;
            Number = CardFormat.Number(card.Number);
            Expiry = CardFormat.Expiry(card.ExpiryMonth + card.ExpiryYear);
            Cvv = card.Cvv;
            Notes = card.Notes;
        }
        else if (existing is not null && generic is not null)
        {
            CardName = existing.Name;
            Notes = generic.Notes;
        }

        SelectedSwatch = existing is not null ? Math.Clamp(existing.Accent + 1, 0, 8) : 0;
        TagsText = existing is not null ? DisplayTags(existing.Tags) : string.Empty;
        foreach (var fo in FolderOptionBuilder.Flatten(
                     AppServices.Database.ListFolders(),
                     noneLabel: "No folder"))
            Folders.Add(fo);
        SelectedFolder = Folders.FirstOrDefault(f => f.Id == (existing?.FolderId ?? string.Empty))
            ?? Folders.FirstOrDefault();

        foreach (var def in kind == EntryKind.Card
                     ? Array.Empty<EntryFieldDef>()
                     : EntryKinds.For(kind).Fields)
            TemplateFields.Add(new EntryFieldRowViewModel(def.Label,
                generic?.Fields.FirstOrDefault(f => f.Label == def.Label)?.Value ?? string.Empty));

        var secrets = IsCard
            ? card?.Secrets ?? new System.Collections.Generic.List<SecretEntry>()
            : generic?.Secrets ?? new System.Collections.Generic.List<SecretEntry>();
        foreach (var s in secrets)
            SecretEntries.Add(NewSecretRow(s.Name, s.Value));
        if (SecretEntries.Count == 0)
            SecretEntries.Add(NewSecretRow(string.Empty, string.Empty));

        _updating = false;

        BuildSwatches();
        RefreshSwatchSelection();
        OnCardNameChanged(CardName);
    }

    private string NumberDigits => new(Number.Where(char.IsDigit).ToArray());
    private string ExpiryDigits => new(Expiry.Where(char.IsDigit).ToArray());
    private int AccentIndex => SelectedSwatch - 1;

    private static string NormalizeTags(string raw)
        => string.Join(",",
            raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Select(t => t.ToLowerInvariant())
               .Where(t => t.Length > 0)
               .Distinct()
               .Take(8));

    private static string DisplayTags(string stored)
        => string.Join(", ", stored.Split(',', StringSplitOptions.RemoveEmptyEntries));

    // ---------- live formatting (card kind) ----------

    partial void OnNumberChanged(string value)
    {
        if (_updating) return;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > 19) digits = digits[..19];
        _updating = true;
        Number = CardFormat.Number(digits);
        _updating = false;
        Recompute();
    }

    partial void OnExpiryChanged(string value)
    {
        if (_updating) return;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > 4) digits = digits[..4];
        _updating = true;
        Expiry = CardFormat.Expiry(digits);
        _updating = false;
        Recompute();
    }

    partial void OnCvvChanged(string value)
    {
        if (_updating) return;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length > 4) digits = digits[..4];
        _updating = true;
        Cvv = digits;
        _updating = false;
        Recompute();
    }

    partial void OnCardNameChanged(string value) => RecomputeCanSaveOnly();
    partial void OnHolderChanged(string value) => Recompute();
    partial void OnNotesChanged(string value) => RecomputeCanSaveOnly();

    private void RecomputeCanSaveOnly()
    {
        var can = CardName.Trim().Length > 0 && (IsCard
            ? HolderValid && NumberOkay && ExpiryOkay && CvvOkay
            : true);
        if (can == CanSave) return;
        CanSave = can;
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void Recompute()
    {
        UpdatePreviewColor();

        if (!IsCard) { RecomputeCanSaveOnly(); return; }

        var digits = NumberDigits;
        var brand = CardBrandInfo.Detect(digits);

        var label = digits.Length >= 2 ? CardBrandInfo.DisplayName(brand).ToUpperInvariant() : string.Empty;
        if (label != _brandLabel)
        {
            _brandLabel = label;
            OnPropertyChanged(nameof(BrandLabel));
        }

        var state = CardBrandInfo.NumberState(digits);
        NumberHint = digits.Length == 0 ? "Enter the digits printed on the card" : state.message;
        NumberOkay = digits.Length > 0 && state.valid;

        var now = DateTime.Now;
        var (month, year) = CardFormat.ParseExpiry(Expiry);
        if (ExpiryDigits.Length < 4)
        {
            ExpiryHint = "MM / YY";
            ExpiryOkay = false;
        }
        else if (month is < 1 or > 12)
        {
            ExpiryHint = "Month must be between 01 and 12";
            ExpiryOkay = false;
        }
        else if (year < now.Year || (year == now.Year && month < now.Month))
        {
            ExpiryHint = "This card has expired";
            ExpiryOkay = false;
        }
        else if (year == now.Year && month == now.Month)
        {
            ExpiryHint = "Expires this month";
            ExpiryOkay = true;
        }
        else
        {
            ExpiryHint = "Card is active";
            ExpiryOkay = true;
        }

        var need = CardBrandInfo.CvvLength(brand);
        if (Cvv.Length == 0)
        {
            CvvHint = digits.Length < 4 ? "Security code" : $"Security code ({need} digits)";
            CvvOkay = false;
        }
        else if (Cvv.Length != need && digits.Length >= 4)
        {
            CvvHint = $"This card brand uses {need} digits";
            CvvOkay = false;
        }
        else if (Cvv.Length is >= 3 and <= 4)
        {
            CvvHint = "Looks right";
            CvvOkay = true;
        }
        else
        {
            CvvHint = "3 to 4 digits";
            CvvOkay = false;
        }

        if (Holder.Length == 0)
        {
            HolderHint = "The name embossed on the card";
            HolderOkay = false;
        }
        else if (HolderValid)
        {
            HolderHint = "Matches the embossed text";
            HolderOkay = true;
        }
        else
        {
            HolderHint = "Letters and spaces only (2–40 characters)";
            HolderOkay = false;
        }

        OnPropertyChanged(nameof(NumberHintBrush));
        OnPropertyChanged(nameof(ExpiryHintBrush));
        OnPropertyChanged(nameof(CvvHintBrush));
        OnPropertyChanged(nameof(HolderHintBrush));

        RecomputeCanSaveOnly();
    }

    private bool HolderValid
    {
        get
        {
            var t = Holder.Trim();
            return t.Length is >= 2 and <= 40 && HolderNameRegex.IsMatch(t);
        }
    }

    private void UpdatePreviewColor()
    {
        (string start, string end) palette = ConfigPalette();
        PreviewColorStart = Color.Parse(palette.start);
        PreviewColorEnd = Color.Parse(palette.end);
        OnPropertyChanged(nameof(PreviewColorStart));
        OnPropertyChanged(nameof(PreviewColorEnd));
    }

    private (string start, string end) ConfigPalette()
    {
        if (AccentIndex is >= 0 and < 8 && AccentIndex < CardBrandInfo.CustomPalettes.Count)
            return CardBrandInfo.CustomPalettes[AccentIndex];
        return (EntryKinds.GraphiteStart, EntryKinds.GraphiteEnd);
    }

    // ---------- swatches ----------

    public void SelectSwatch(int index)
    {
        if (index is < 0 or > 8) return;
        SelectedSwatch = index;
        RefreshSwatchSelection();
        Recompute();
    }

    private void RefreshSwatchSelection()
    {
        foreach (var swatch in Swatches)
            swatch.SetSelected(swatch.Index == SelectedSwatch);
    }

    private void BuildSwatches()
    {
        Swatches.Add(new SwatchViewModel(0, "Auto", "#8891AA", SelectSwatch));
        for (var i = 0; i < CardBrandInfo.CustomPalettes.Count; i++)
            Swatches.Add(new SwatchViewModel(i + 1, string.Empty, CardBrandInfo.CustomPalettes[i].start, SelectSwatch));
    }

    // ---------- secret entries ----------

    private SecretEntryViewModel NewSecretRow(string name, string value)
        => new(name, value, row => SecretEntries.Remove(row));

    [RelayCommand]
    private void AddSecretEntry()
    {
        if (SecretEntries.Count >= MaxSecretEntries) return;
        SecretEntries.Add(NewSecretRow(string.Empty, string.Empty));
    }

    // ---------- actions ----------

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var collectedSecrets = SecretEntries
            .Select(r => new SecretEntry { Name = r.Name.Trim(), Value = r.Value.Trim() })
            .Where(s => s.Name.Length > 0 || s.Value.Length > 0)
            .Take(MaxSecretEntries)
            .ToList();

        if (IsCard)
        {
            var digits = NumberDigits;
            var exp = ExpiryDigits;
            var data = new CardSecureData
            {
                Holder = Holder.Trim(),
                Number = digits,
                ExpiryMonth = exp.Length >= 2 ? exp[..2] : string.Empty,
                ExpiryYear = exp.Length >= 4 ? exp[2..] : string.Empty,
                Cvv = Cvv,
                Notes = Notes.Trim(),
                Secrets = collectedSecrets,
            };

            var brand = CardBrandInfo.Detect(digits).ToString().ToLowerInvariant();
            var accent = AccentIndex;
            var tags = NormalizeTags(TagsText);

            if (_existing is null)
            {
                var entry = AppServices.Database.CreateCard(CardName.Trim(), brand, accent, data, tags);
                entry.FolderId = SelectedFolder?.Id ?? string.Empty;
                AppServices.Database.InsertEntry(entry);
            }
            else
            {
                _existing.FolderId = SelectedFolder?.Id ?? string.Empty;
                AppServices.Database.UpdateCard(_existing, CardName.Trim(), brand, accent, data, tags);
            }
        }
        else
        {
            var data = new EntrySecureData
            {
                Notes = Notes.Trim(),
                Fields = TemplateFields
                    .Where(f => f.Value.Trim().Length > 0)
                    .Select(f => new EntryField { Label = f.Label, Value = f.Value.Trim() })
                    .ToList(),
                Secrets = collectedSecrets,
            };

            var accent = AccentIndex;
            var tags = NormalizeTags(TagsText);
            if (_existing is null)
            {
                var entry = AppServices.Database.CreateEntry(CardName.Trim(), Kind, accent, data, tags);
                entry.FolderId = SelectedFolder?.Id ?? string.Empty;
                AppServices.Database.InsertEntry(entry);
            }
            else
            {
                _existing.FolderId = SelectedFolder?.Id ?? string.Empty;
                AppServices.Database.UpdateEntryData(_existing, CardName.Trim(), Kind, accent, data, tags);
            }
        }

        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke();
}

public partial class SwatchViewModel : ViewModelBase
{
    private readonly Action<int> _onSelect;

    [ObservableProperty] private bool isSelected;

    public SwatchViewModel(int index, string label, string color, Action<int> onSelect)
    {
        Index = index;
        Label = label;
        _onSelect = onSelect;
        FillBrush = new SolidColorBrush(Color.Parse(color));
        SelectCommand = new RelayCommand(() => _onSelect(Index));
    }

    public int Index { get; }
    public string Label { get; }
    public IBrush FillBrush { get; }
    public IRelayCommand SelectCommand { get; }

    public void SetSelected(bool selected) => IsSelected = selected;
}

public partial class SecretEntryViewModel : ViewModelBase
{
    private readonly Action<SecretEntryViewModel> _remove;

    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string value = string.Empty;

    public SecretEntryViewModel(string name, string value, Action<SecretEntryViewModel> remove)
    {
        Name = name;
        Value = value;
        _remove = remove;
        RemoveCommand = new RelayCommand(() => _remove(this));
    }

    public IRelayCommand RemoveCommand { get; }
}

public partial class EntryFieldRowViewModel : ViewModelBase
{
    [ObservableProperty] private string value = string.Empty;

    public EntryFieldRowViewModel(string label, string value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
}
