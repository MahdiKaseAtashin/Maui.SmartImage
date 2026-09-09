namespace Maui.SmartImage.Services;

public interface IImageLoader
{
    Task<ImageLoadResult> LoadAsync(ImageLoadRequest request, CancellationToken cancellationToken);
}
