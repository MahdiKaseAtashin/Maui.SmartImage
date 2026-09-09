namespace Maui.SmartImage.Services;

public sealed record ImageLoadRequest
{
    public required string Url { get; init; }

    public ImageCachePolicy CachePolicy { get; init; } = ImageCachePolicy.MemoryAndDisk;

    public TimeSpan? CacheDuration { get; init; }

    public TimeSpan? Timeout { get; init; }

    public long? MaxImageSizeBytes { get; init; }

    public int MaxRetryCount { get; init; } = 2;

    public bool EnableAutomaticRetry { get; init; } = true;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
}
