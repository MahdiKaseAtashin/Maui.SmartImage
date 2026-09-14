using Microsoft.Maui.Dispatching;

namespace Maui.SmartImage.Tests;

/// <summary>
/// Minimal dispatcher so MAUI bindable property changes work in unit tests.
/// </summary>
internal sealed class TestDispatcher : IDispatcher
{
    public bool IsDispatchRequired => false;

    public bool Dispatch(Action action)
    {
        action();
        return true;
    }

    public bool DispatchDelayed(TimeSpan delay, Action action)
    {
        action();
        return true;
    }

    public IDispatcherTimer CreateTimer() => new TestDispatcherTimer();
}

internal sealed class TestDispatcherProvider : IDispatcherProvider
{
    private readonly IDispatcher _dispatcher = new TestDispatcher();

    public IDispatcher GetForCurrentThread() => _dispatcher;
}

internal sealed class TestDispatcherTimer : IDispatcherTimer
{
    public TimeSpan Interval { get; set; }
    public bool IsRepeating { get; set; }
    public bool IsRunning { get; private set; }

    public event EventHandler? Tick
    {
        add { }
        remove { }
    }

    public void Start() => IsRunning = true;

    public void Stop() => IsRunning = false;
}

internal static class MauiTestBootstrap
{
    private static int _initialized;

    public static void EnsureInitialized()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        DispatcherProvider.SetCurrent(new TestDispatcherProvider());
    }
}
