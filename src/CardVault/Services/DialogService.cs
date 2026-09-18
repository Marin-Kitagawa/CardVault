using System.Threading.Tasks;
using Avalonia.Controls;
using CardVault.Views;

namespace CardVault.Services;

public static class DialogService
{
    private static Window ResolveOwner(Window? owner) => owner ?? AppServices.MainWindow;

    public static async Task ShowAsync(Window? owner, string title, string message, string ok = "OK")
    {
        var window = new MessageWindow(title, message, ok, null);
        await window.ShowDialog(ResolveOwner(owner));
    }

    public static async Task<bool> ConfirmAsync(
        Window? owner,
        string title,
        string message,
        string ok = "Continue",
        string cancel = "Cancel",
        bool danger = false)
    {
        var window = new MessageWindow(title, message, ok, cancel, danger);
        await window.ShowDialog(ResolveOwner(owner));
        return window.Result;
    }

    public static async Task<string?> AskPassphraseAsync(
        Window? owner,
        string title,
        string subtitle,
        bool requireConfirm)
    {
        var window = new PassphraseWindow(title, subtitle, requireConfirm);
        await window.ShowDialog(ResolveOwner(owner));
        return window.Result;
    }
}