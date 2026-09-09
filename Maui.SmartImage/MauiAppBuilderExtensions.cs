using Maui.SmartImage.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Maui.SmartImage;

public static class MauiAppBuilderExtensions
{
    /// <summary>
    /// Registers the services required by <see cref="Controls.SmartImage"/>: a two-tier memory+disk
    /// <see cref="IImageCache"/> and a deduplicating, retrying <see cref="IImageLoader"/>.
    /// Call this once in your <c>MauiProgram.cs</c>, e.g. <c>builder.UseSmartImage();</c>.
    /// </summary>
    public static MauiAppBuilder UseSmartImage(this MauiAppBuilder builder, Action<SmartImageOptions>? configureOptions = null)
    {
        SmartImageOptions options = new();
        configureOptions?.Invoke(options);

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(options);

        builder.Services.AddSingleton<IImageCache>(sp => new ImageCache(
            sp.GetRequiredService<IMemoryCache>(),
            options.DiskCacheDirectory ?? Path.Combine(FileSystem.CacheDirectory, "maui_smart_image_cache"),
            sp.GetRequiredService<TimeProvider>()));

        builder.Services.AddHttpClient<IImageLoader, ImageLoader>(client =>
            {
                client.Timeout = options.DefaultTimeout;
                options.ConfigureHttpClient?.Invoke(client);
            })
            .ConfigurePrimaryHttpMessageHandler(options.PrimaryHttpMessageHandlerFactory ?? (() => new HttpClientHandler()));

        return builder;
    }
}
