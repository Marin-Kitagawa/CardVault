using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;

namespace CardVault.ViewModels;

public sealed record QrValueOption(string Label, string Value);

/// <summary>
/// Lets the user share a single, already-revealed value as a scannable QR code.
/// Only rows that were explicitly revealed on the details screen appear here;
/// masked values are never offered.
/// </summary>
public partial class QrViewModel : ViewModelBase
{
    private static readonly QRCodeGenerator Generator = new();

    private readonly List<QrValueOption> _options;

    public event Action? RequestClose;

    public QrViewModel(IEnumerable<QrValueOption> options)
    {
        _options = options.ToList();
        if (Options.Count > 0)
            SelectedOption = Options[0];
    }

    public IReadOnlyList<QrValueOption> Options => _options;

    [RelayCommand]
    private void Done() => RequestClose?.Invoke();

    [ObservableProperty] private QrValueOption? selectedOption;

    [ObservableProperty] private IImage? qrSource;

    [ObservableProperty] private string status = "Generating…";

    partial void OnSelectedOptionChanged(QrValueOption? value)
    {
        if (value is null)
        {
            QrSource = null;
            Status = "Nothing selected.";
            return;
        }

        try
        {
            var data = Generator.CreateQrCode(value.Value, QRCodeGenerator.ECCLevel.M);
            using var png = new PngByteQRCode(data);
            using var stream = new MemoryStream(png.GetGraphic(6));
            QrSource = new Bitmap(stream);
            Status = $"Sharing: {value.Label}";
        }
        catch (Exception ex)
        {
            QrSource = null;
            Status = $"Could not generate a QR code: {ex.Message}";
        }
    }
}