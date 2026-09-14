namespace Maui.SmartImage.Services;

/// <summary>
/// Parameters for a remote image download.
/// </summary>
public sealed record ImageLoadRequest
{
    /// <summary>
    /// Absolute http(s) URL to download.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Cache tiers to use for this request.
    /// </summary>
    public ImageCachePolicy CachePolicy { get; init; } = ImageCachePolicy.MemoryAndDisk;

    /// <summary>
    /// Cache entry lifetime. When <see langword="null"/>, <see cref="SmartImageOptions.DefaultCacheDuration"/> is used.
    /// </summary>
    public TimeSpan? CacheDuration { get; init; }

    /// <summary>
    /// Per-attempt download timeout. When <see langword="null"/>, <see cref="SmartImageOptions.DefaultTimeout"/> is used.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Maximum accepted response body size. When <see langword="null"/>, <see cref="SmartImageOptions.DefaultMaxImageSizeBytes"/> is used.
    /// </summary>
    public long? MaxImageSizeBytes { get; init; }

    /// <summary>
    /// Number of automatic retries after the first attempt.
    /// </summary>
    public int MaxRetryCount { get; init; } = 2;

    /// <summary>
    /// Whether transient failures should be retried.
    /// </summary>
    public bool EnableAutomaticRetry { get; init; } = true;

    /// <summary>
    /// Base delay for exponential backoff between retries.
    /// </summary>
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}
