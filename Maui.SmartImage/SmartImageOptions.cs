namespace Maui.SmartImage;

public sealed class SmartImageOptions
{
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
    /// Root directory for the disk cache tier. Defaults to <c>FileSystem.CacheDirectory/maui_smart_image_cache</c>.
    /// </summary>
    public string? DiskCacheDirectory { get; set; }
}
