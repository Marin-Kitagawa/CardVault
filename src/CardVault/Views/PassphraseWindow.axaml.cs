using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CardVault.Views;

public partial class PassphraseWindow : Window
{
    public string? Result { get; private set; }

    public PassphraseWindow()
    {
        InitializeComponent();
    }

    public PassphraseWindow(string title, string subtitle, bool requireConfirm)
        : this()
    {
        Title = title;
        SubtitleText.Text = subtitle;
        ConfirmSection.IsVisible = requireConfirm;

        OkButton.Click += OnOk;
        CancelButton.Click += OnCancel;
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var passphrase = EntryPassword.Text ?? string.Empty;
        if (passphrase.Length < 8)
        {
            ErrorText.Text = "Use at least 8 characters.";
            ErrorPanel.IsVisible = true;
            return;
        }

        if (ConfirmSection.IsVisible && passphrase != EntryConfirm.Text)
        {
            ErrorText.Text = "Passphrases do not match.";
            ErrorPanel.IsVisible = true;
            return;
        }

        Result = passphrase;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}