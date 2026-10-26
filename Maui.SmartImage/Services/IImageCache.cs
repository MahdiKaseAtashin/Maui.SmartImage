namespace Maui.SmartImage.Services;

/// <summary>
/// Two-tier (memory + disk) cache used by <see cref="IImageLoader"/> for remote image payloads.
/// </summary>
public interface IImageCache
{
    /// <summary>
    /// Attempts to read cached image bytes for <paramref name="key"/> according to <paramref name="policy"/>.
    /// </summary>
    Task<byte[]?> TryGetAsync(string key, ImageCachePolicy policy, CancellationToken cancellationToken);

    /// <summary>
    /// Stores image bytes for <paramref name="key"/> according to <paramref name="policy"/>.
    /// When <paramref name="duration"/> is <see langword="null"/>, the cache's default entry duration is used.
    /// </summary>
    Task SetAsync(string key, byte[] data, ImageCachePolicy policy, TimeSpan? duration, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a single cache entry from memory and disk.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Clears all memory and disk cache entries managed by this instance.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Evicts in-memory cache entries to reduce process memory. Disk entries are left unchanged.
    /// </summary>
    /// <param name="percentage">
    /// Compact fraction in the range 0–1. <c>1.0</c> removes all in-memory entries.
    /// </param>
    void TrimMemory(double percentage = 1.0)
    {
    }
}
