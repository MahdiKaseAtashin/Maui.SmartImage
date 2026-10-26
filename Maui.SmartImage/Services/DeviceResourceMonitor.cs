using Microsoft.Maui.Devices;

namespace Maui.SmartImage.Services;

/// <summary>
/// Default <see cref="IDeviceResourceMonitor"/> backed by <see cref="Battery.EnergySaverStatus"/>.
/// Trims the in-memory image cache when energy saver turns on.
/// </summary>
internal sealed class DeviceResourceMonitor : IDeviceResourceMonitor, IDisposable
{
    private readonly IImageCache _cache;
    private readonly object _gate = new();
    private bool _subscribed;

    public DeviceResourceMonitor(IImageCache cache)
    {
        _cache = cache;

        try
        {
            Battery.EnergySaverStatusChanged += OnEnergySaverStatusChanged;
            _subscribed = true;
        }
        catch (Exception)
        {
            // Headless tests and platforms without battery APIs.
        }
    }

    /// <inheritdoc />
    public bool IsConstrained
    {
        get
        {
            try
            {
                return Battery.EnergySaverStatus == EnergySaverStatus.On;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? ConstraintsChanged;

    private void OnEnergySaverStatusChanged(object? sender, EnergySaverStatusChangedEventArgs e)
    {
        if (e.EnergySaverStatus == EnergySaverStatus.On)
        {
            _cache.TrimMemory(0.75);
        }

        ConstraintsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (!_subscribed)
            {
                return;
            }

            try
            {
                Battery.EnergySaverStatusChanged -= OnEnergySaverStatusChanged;
            }
            catch (Exception)
            {
                // Ignore unsubscribe failures.
            }

            _subscribed = false;
        }
    }
}
