namespace Maui.SmartImage.Services;

public interface IImageCache
{
    Task<byte[]?> TryGetAsync(string key, ImageCachePolicy policy, CancellationToken cancellationToken);

    Task SetAsync(string key, byte[] data, ImageCachePolicy policy, TimeSpan? duration, CancellationToken cancellationToken);
}
