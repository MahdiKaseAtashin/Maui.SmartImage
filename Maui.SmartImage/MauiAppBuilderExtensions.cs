using Maui.SmartImage.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maui.SmartImage;

/// <summary>
/// MAUI host registration helpers for SmartImage services.
/// </summary>
public static class MauiAppBuilderExtensions
{
    internal const string HttpClientName = "Maui.SmartImage";

    /// <summary>
    /// Registers the services required by <see cref="Controls.SmartImage"/>: a two-tier memory+disk
    /// <see cref="IImageCache"/> and a deduplicating, retrying <see cref="IImageLoader"/>.
    /// Call this once in your <c>MauiProgram.cs</c>, e.g. <c>builder.UseSmartImage();</c>.
    /// </summary>
    /// <remarks>
    /// Uses a private <see cref="IMemoryCache"/> instance owned by <see cref="ImageCache"/> so
    /// <see cref="IImageCache.ClearAsync"/> cannot wipe the host app's shared memory cache.
    /// </remarks>
    public static MauiAppBuilder UseSmartImage(this MauiAppBuilder builder, Action<SmartImageOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        SmartImageOptions options = new();
        configureOptions?.Invoke(options);

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(options);

        builder.Services.AddSingleton<IImageCache>(sp =>
        {
            MemoryCache memoryCache = new(new MemoryCacheOptions
            {
                SizeLimit = options.MemoryCacheSizeLimitBytes
            });

            return new ImageCache(
                memoryCache,
                options.DiskCacheDirectory ?? Path.Combine(FileSystem.CacheDirectory, "maui_smart_image_cache"),
                sp.GetRequiredService<TimeProvider>(),
                options.DefaultCacheDuration,
                options.MaxDiskCacheSizeBytes,
                ownsMemoryCache: true);
        });

        builder.Services.AddSingleton<IDeviceResourceMonitor>(sp =>
            new DeviceResourceMonitor(sp.GetRequiredService<IImageCache>()));

        builder.Services.AddHttpClient(HttpClientName, client =>
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                options.ConfigureHttpClient?.Invoke(client);
            })
            .ConfigurePrimaryHttpMessageHandler(
                options.PrimaryHttpMessageHandlerFactory
                ?? (() => new HttpClientHandler
                {
                    MaxConnectionsPerServer = Math.Max(1, options.MaxConcurrentDownloads)
                }));

        builder.Services.AddSingleton<IImageLoader>(sp =>
            new ImageLoader(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IImageCache>(),
                options,
                sp.GetService<ILogger<ImageLoader>>(),
                sp.GetService<IDeviceResourceMonitor>()));

        return builder;
    }
}
