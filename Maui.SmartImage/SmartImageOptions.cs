namespace Maui.SmartImage;

/// <summary>
/// Global configuration for <see cref="Controls.SmartImage"/> remote loading and caching.
/// Configure via <see cref="MauiAppBuilderExtensions.UseSmartImage"/>.
/// </summary>
public sealed class SmartImageOptions
{
    /// <summary>
    /// Default memory-cache size limit (64 MB).
    /// </summary>
    public const long DefaultMemoryCacheSizeLimitBytes = 64L * 1024 * 1024;

    /// <summary>
    /// Default disk-cache size budget (256 MB).
    /// </summary>
    public const long DefaultMaxDiskCacheSizeBytes = 256L * 1024 * 1024;

    /// <summary>
    /// Default maximum download size (15 MB).
    /// </summary>
    public const long DefaultMaxImageSizeBytesValue = 15L * 1024 * 1024;

    /// <summary>
    /// Factory for the primary <see cref="HttpMessageHandler"/> used for remote image downloads.
    /// Override this to inject dev-certificate trust, a proxy, or any other transport-level behavior
    /// your app needs. Defaults to a plain <see cref="HttpClientHandler"/>.
    /// </summary>
    public Func<HttpMessageHandler>? PrimaryHttpMessageHandlerFactory { get; set; }

    /// <summary>
    /// Called once when the shared <see cref="HttpClient"/> is configured, e.g. to add default headers.
    /// </summary>
    public Action<HttpClient>? ConfigureHttpClient { get; set; }

    /// <summary>
    /// Default per-request timeout used when <c>SmartImage.Timeout</c> is not set. Defaults to 15 seconds.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Default cache entry lifetime used when <c>SmartImage.CacheDuration</c> is not set. Defaults to 7 days.
    /// </summary>
    public TimeSpan DefaultCacheDuration { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Maximum total size of the in-memory image cache. Defaults to 64 MB.
    /// </summary>
    public long MemoryCacheSizeLimitBytes { get; set; } = DefaultMemoryCacheSizeLimitBytes;

    /// <summary>
    /// Maximum total size of on-disk cached images. Oldest entries are evicted when exceeded. Defaults to 256 MB.
    /// </summary>
    public long MaxDiskCacheSizeBytes { get; set; } = DefaultMaxDiskCacheSizeBytes;

    /// <summary>
    /// Default maximum download size used when <c>SmartImage.MaxImageSizeBytes</c> is not set. Defaults to 15 MB.
    /// </summary>
    public long DefaultMaxImageSizeBytes { get; set; } = DefaultMaxImageSizeBytesValue;

    /// <summary>
    /// Root directory for the disk cache tier. Defaults to <c>FileSystem.CacheDirectory/maui_smart_image_cache</c>.
    /// </summary>
    public string? DiskCacheDirectory { get; set; }
}
