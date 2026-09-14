namespace Maui.SmartImage.Services;

/// <summary>
/// Loads remote images with caching, deduplication, and retry support.
/// </summary>
public interface IImageLoader
{
    /// <summary>
    /// Loads an image described by <paramref name="request"/>.
    /// </summary>
    Task<ImageLoadResult> LoadAsync(ImageLoadRequest request, CancellationToken cancellationToken);
}
