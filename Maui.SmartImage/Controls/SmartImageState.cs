namespace Maui.SmartImage.Controls;

/// <summary>
/// Visual/load state exposed by <see cref="SmartImage"/>.
/// </summary>
public enum SmartImageState
{
    /// <summary>No source, or source cleared.</summary>
    Idle,

    /// <summary>A remote load is in progress.</summary>
    Loading,

    /// <summary>A local or remote image is displayed.</summary>
    Loaded,

    /// <summary>The latest remote load failed.</summary>
    Failed
}
