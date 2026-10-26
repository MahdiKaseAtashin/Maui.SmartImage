using Maui.SmartImage.Services;

namespace Maui.SmartImage.Tests;

internal sealed class FakeDeviceResourceMonitor : IDeviceResourceMonitor
{
    public bool IsConstrained { get; set; }

    public event EventHandler? ConstraintsChanged;

    public void RaiseConstraintsChanged()
    {
        ConstraintsChanged?.Invoke(this, EventArgs.Empty);
    }
}
