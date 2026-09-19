using System;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using CardVault.Converters;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(CardVault.Tests.Ui.TestApp))]

namespace CardVault.Tests.Ui;

public class TestApp : Application
{
    public override void Initialize()
    {
        base.Initialize();
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://CardVault/Styles/Theme.axaml")) { Source = new Uri("avares://CardVault/Styles/Theme.axaml") });

        Resources["NonEmptyToVis"] = new NonEmptyToVisibilityConverter();
        Resources["StringEmptyToBool"] = new StringEmptyToBoolConverter();
        Resources["InverseBool"] = new InverseBoolConverter();
        Resources["BoolToThickness"] = new BoolToThicknessConverter();
        Resources["StringToBrush"] = new StringToBrushConverter();
    }
}