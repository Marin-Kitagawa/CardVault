using System;
using Avalonia.Threading;

namespace CardVault.Services;

/// <summary>
/// Locks the vault after a configurable period of user inactivity.
/// </summary>
public sealed class AutoLockService
{
    private readonly DispatcherTimer _timer;
    private DateTime _lastActivity = DateTime.UtcNow;
    private Func<int> _minutesProvider = () => 5;

    public AutoLockService()
    {
        _timer = new DispatcherTimer(TimeSpan.FromMinutes(1), DispatcherPriority.Background, OnTick);
    }

    public void Start(Func<int> minutesProvider)
    {
        _minutesProvider = minutesProvider;
        _timer.Start();
    }

    public void Touch() => _lastActivity = DateTime.UtcNow;

    private void OnTick(object? sender, EventArgs e)
    {
        if (AppServices.Session.IsUnlocked &&
            DateTime.UtcNow - _lastActivity >= TimeSpan.FromMinutes(_minutesProvider()))
        {
            AppServices.Session.Lock();
        }
    }
}