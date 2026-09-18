using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CardVault.Views;

public partial class CardFaceView : UserControl
{
    public static readonly StyledProperty<Color> ColorStartProperty =
        AvaloniaProperty.Register<CardFaceView, Color>(nameof(ColorStart), Colors.Transparent);

    public static readonly StyledProperty<Color> ColorEndProperty =
        AvaloniaProperty.Register<CardFaceView, Color>(nameof(ColorEnd), Colors.Transparent);

    public static readonly StyledProperty<IBrush> FaceBrushProperty =
        AvaloniaProperty.Register<CardFaceView, IBrush>(nameof(FaceBrush));

    public static readonly StyledProperty<IBrush> FaceForegroundProperty =
        AvaloniaProperty.Register<CardFaceView, IBrush>(nameof(FaceForeground));

    public static readonly StyledProperty<IBrush> BrandPillBackgroundProperty =
        AvaloniaProperty.Register<CardFaceView, IBrush>(nameof(BrandPillBackground));

    public static readonly StyledProperty<string> CardNameTextProperty =
        AvaloniaProperty.Register<CardFaceView, string>(nameof(CardNameText), string.Empty);

    public static readonly StyledProperty<string> BrandTextProperty =
        AvaloniaProperty.Register<CardFaceView, string>(nameof(BrandText), string.Empty);

    public static readonly StyledProperty<string> NumberTextProperty =
        AvaloniaProperty.Register<CardFaceView, string>(nameof(NumberText), string.Empty);

    public static readonly StyledProperty<string> HolderTextProperty =
        AvaloniaProperty.Register<CardFaceView, string>(nameof(HolderText), string.Empty);

    public static readonly StyledProperty<string> ExpiryTextProperty =
        AvaloniaProperty.Register<CardFaceView, string>(nameof(ExpiryText), string.Empty);

    public static readonly StyledProperty<bool> ShowChipProperty =
        AvaloniaProperty.Register<CardFaceView, bool>(nameof(ShowChip), true);

    public static readonly StyledProperty<bool> ShowContactlessProperty =
        AvaloniaProperty.Register<CardFaceView, bool>(nameof(ShowContactless), true);

    public CardFaceView()
    {
        InitializeComponent();
        RebuildBrush();
    }

    static CardFaceView()
    {
        ColorStartProperty.Changed.AddClassHandler<CardFaceView>((o, _) => o.RebuildBrush());
        ColorEndProperty.Changed.AddClassHandler<CardFaceView>((o, _) => o.RebuildBrush());
    }

    public Color ColorStart
    {
        get => GetValue(ColorStartProperty);
        set => SetValue(ColorStartProperty, value);
    }

    public Color ColorEnd
    {
        get => GetValue(ColorEndProperty);
        set => SetValue(ColorEndProperty, value);
    }

    public IBrush FaceBrush
    {
        get => GetValue(FaceBrushProperty);
        private set => SetValue(FaceBrushProperty, value);
    }

    public IBrush FaceForeground
    {
        get => GetValue(FaceForegroundProperty);
        private set => SetValue(FaceForegroundProperty, value);
    }

    public IBrush BrandPillBackground
    {
        get => GetValue(BrandPillBackgroundProperty);
        private set => SetValue(BrandPillBackgroundProperty, value);
    }

    public string CardNameText { get => GetValue(CardNameTextProperty); set => SetValue(CardNameTextProperty, value); }
    public string BrandText { get => GetValue(BrandTextProperty); set => SetValue(BrandTextProperty, value); }
    public string NumberText { get => GetValue(NumberTextProperty); set => SetValue(NumberTextProperty, value); }
    public string HolderText { get => GetValue(HolderTextProperty); set => SetValue(HolderTextProperty, value); }
    public string ExpiryText { get => GetValue(ExpiryTextProperty); set => SetValue(ExpiryTextProperty, value); }
    public bool ShowChip { get => GetValue(ShowChipProperty); set => SetValue(ShowChipProperty, value); }
    public bool ShowContactless { get => GetValue(ShowContactlessProperty); set => SetValue(ShowContactlessProperty, value); }

    private static readonly Color InkColor = Color.FromRgb(26, 23, 19);
    private static readonly Color PaperColor = Color.FromRgb(250, 251, 253);

    private void RebuildBrush()
    {
        var start = ColorStart;
        var end = ColorEnd;
        if (start.Equals(Colors.Transparent) || end.Equals(Colors.Transparent)) return;

        var gradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        gradient.GradientStops.Add(new GradientStop(start, 0));
        gradient.GradientStops.Add(new GradientStop(end, 1));
        FaceBrush = gradient;

        var luminance = (0.299 * end.R + 0.587 * end.G + 0.114 * end.B) / 255f;
        var dark = luminance < 150f;
        FaceForeground = new SolidColorBrush(dark ? PaperColor : InkColor);
        BrandPillBackground = new SolidColorBrush(
            dark ? Color.FromArgb(70, 0, 0, 0) : Color.FromArgb(24, 255, 255, 255));
    }
}