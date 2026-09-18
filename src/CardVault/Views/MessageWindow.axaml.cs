using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CardVault.Views;

public partial class MessageWindow : Window
{
    public bool Result { get; private set; }

    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message, string ok, string? cancel, bool danger = false)
        : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        OkButton.Content = ok;
        OkButton.Click += OnOk;

        if (cancel is null)
        {
            CancelButton.IsVisible = false;
        }
        else
        {
            CancelButton.Content = cancel;
            CancelButton.Click += OnCancel;
        }

        if (danger)
        {
            OkButton.Classes.Remove("tprimary");
            OkButton.Classes.Add("danger");
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}