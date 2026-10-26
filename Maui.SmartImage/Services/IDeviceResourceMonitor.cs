namespace Maui.SmartImage.Services;

/// <summary>
/// Observes device-level constraints such as energy-saver / low-power mode.
/// </summary>
internal interface IDeviceResourceMonitor
{
    /// <summary>
    /// <see langword="true"/> when the OS is asking apps to reduce work (energy saver, low power).
    /// </summary>
    bool IsConstrained { get; }

    /// <summary>
    /// Raised when <see cref="IsConstrained"/> may have changed.
    /// </summary>
    event EventHandler? ConstraintsChanged;
}
