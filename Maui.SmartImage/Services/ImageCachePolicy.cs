namespace Maui.SmartImage.Services;

/// <summary>
/// Selects which cache tiers participate in a load.
/// </summary>
public enum ImageCachePolicy
{
    /// <summary>Do not read or write any cache.</summary>
    None,

    /// <summary>Use only the in-memory tier.</summary>
    Memory,

    /// <summary>Use only the on-disk tier.</summary>
    Disk,

    /// <summary>Use memory first, then disk; promote disk hits into memory.</summary>
    MemoryAndDisk
}
