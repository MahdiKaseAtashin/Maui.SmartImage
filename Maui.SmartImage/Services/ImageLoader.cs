using System.Collections.Concurrent;
using System.Net;
using System.Security.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maui.SmartImage.Services;

/// <summary>
/// Default <see cref="IImageLoader"/> with cache lookup, in-flight deduplication, and retry/backoff.
/// </summary>
public sealed class ImageLoader : IImageLoader
{
    internal const int MaxInFlightJoinAttempts = 16;

    private readonly HttpClient? _httpClient;
    private readonly IHttpClientFactory? _httpClientFactory;
    private readonly IImageCache _cache;
    private readonly SmartImageOptions _options;
    private readonly ILogger<ImageLoader> _logger;
    private readonly IDeviceResourceMonitor? _deviceResources;
    private readonly SemaphoreSlim _downloadSlots;
    private readonly ConcurrentDictionary<string, InFlightDownload> _inFlightDownloads = new();
    private readonly Random _jitter = new();

    /// <summary>
    /// Creates a new image loader that uses a caller-owned <see cref="HttpClient"/> (unit tests).
    /// </summary>
    public ImageLoader(
        HttpClient httpClient,
        IImageCache cache,
        SmartImageOptions options,
        ILogger<ImageLoader>? logger = null)
        : this(httpClient, httpClientFactory: null, cache, options, logger, deviceResources: null)
    {
    }

    internal ImageLoader(
        HttpClient httpClient,
        IImageCache cache,
        SmartImageOptions options,
        ILogger<ImageLoader>? logger,
        IDeviceResourceMonitor? deviceResources)
        : this(httpClient, httpClientFactory: null, cache, options, logger, deviceResources)
    {
    }

    internal ImageLoader(
        IHttpClientFactory httpClientFactory,
        IImageCache cache,
        SmartImageOptions options,
        ILogger<ImageLoader>? logger = null,
        IDeviceResourceMonitor? deviceResources = null)
        : this(httpClient: null, httpClientFactory, cache, options, logger, deviceResources)
    {
    }

    private ImageLoader(
        HttpClient? httpClient,
        IHttpClientFactory? httpClientFactory,
        IImageCache cache,
        SmartImageOptions options,
        ILogger<ImageLoader>? logger,
        IDeviceResourceMonitor? deviceResources)
    {
        if (httpClient is null && httpClientFactory is null)
        {
            throw new ArgumentException("An HttpClient or IHttpClientFactory is required.");
        }

        _httpClient = httpClient;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
        _logger = logger ?? NullLogger<ImageLoader>.Instance;
        _deviceResources = deviceResources;
        _downloadSlots = new SemaphoreSlim(
            Math.Max(1, options.MaxConcurrentDownloads),
            Math.Max(1, options.MaxConcurrentDownloads));
    }

    private HttpClient GetHttpClient()
    {
        return _httpClient ?? _httpClientFactory!.CreateClient(MauiAppBuilderExtensions.HttpClientName);
    }

    /// <inheritdoc />
    public async Task<ImageLoadResult> LoadAsync(ImageLoadRequest request, CancellationToken cancellationToken)
    {
        if (!TryValidateUri(request.Url, out Uri uri))
        {
            return ImageLoadResult.Failure(ImageLoadErrorKind.InvalidUrl, $"'{request.Url}' is not a valid http(s) image URL.");
        }

        string cacheKey = request.Url;

        if (request.CachePolicy != ImageCachePolicy.None)
        {
            byte[]? cached = await _cache.TryGetAsync(cacheKey, request.CachePolicy, cancellationToken).ConfigureAwait(false);

            if (cached is not null)
            {
                return ImageLoadResult.Success(cached);
            }
        }

        string inFlightKey = BuildInFlightKey(request);

        for (int joinAttempt = 0; joinAttempt < MaxInFlightJoinAttempts; joinAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            InFlightDownload entry = _inFlightDownloads.GetOrAdd(inFlightKey, static _ => new InFlightDownload());
            Task<ImageLoadResult>? downloadTask = null;
            bool joined = false;

            lock (entry.Gate)
            {
                if (entry.Removed)
                {
                    // Fall through and retry with a replacement entry.
                }
                else
                {
                    entry.WaiterCount++;
                    joined = true;
                    entry.Task ??= DownloadWithRetryAsync(request, uri, cacheKey, inFlightKey, entry);
                    downloadTask = entry.Task;
                }
            }

            if (!joined || downloadTask is null)
            {
                RemoveIfSame(inFlightKey, entry);
                await Task.Yield();
                continue;
            }

            try
            {
                return await downloadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (downloadTask.IsCompletedSuccessfully)
            {
                return await downloadTask.ConfigureAwait(false);
            }
            finally
            {
                ReleaseWaiter(inFlightKey, entry);
            }
        }

        return ImageLoadResult.Failure(
            ImageLoadErrorKind.Unknown,
            "Unable to join the in-flight download.");
    }

    private void ReleaseWaiter(string inFlightKey, InFlightDownload entry)
    {
        lock (entry.Gate)
        {
            entry.WaiterCount--;

            if (entry.WaiterCount > 0 || entry.Task is null || entry.Task.IsCompleted)
            {
                return;
            }

            entry.Removed = true;

            try
            {
                entry.Cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Download already finished and disposed the token source.
            }
        }

        RemoveIfSame(inFlightKey, entry);
    }

    private void RemoveIfSame(string inFlightKey, InFlightDownload entry)
    {
        ((ICollection<KeyValuePair<string, InFlightDownload>>)_inFlightDownloads)
            .Remove(new KeyValuePair<string, InFlightDownload>(inFlightKey, entry));
    }

    private async Task<ImageLoadResult> DownloadWithRetryAsync(
        ImageLoadRequest request,
        Uri uri,
        string cacheKey,
        string inFlightKey,
        InFlightDownload entry)
    {
        try
        {
            bool constrained = _deviceResources?.IsConstrained == true;
            int maxAttempts = request.EnableAutomaticRetry && !constrained
                ? Math.Max(1, request.MaxRetryCount + 1)
                : 1;
            TimeSpan perAttemptTimeout = request.Timeout ?? _options.DefaultTimeout;
            TimeSpan overallBudget = constrained
                ? perAttemptTimeout
                : CalculateOverallBudget(perAttemptTimeout, request.RetryDelay, maxAttempts);

            using CancellationTokenSource overallCts = CancellationTokenSource.CreateLinkedTokenSource(entry.Cts.Token);
            overallCts.CancelAfter(overallBudget);
            ImageLoadResult lastResult = ImageLoadResult.Failure(ImageLoadErrorKind.Unknown);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (overallCts.IsCancellationRequested)
                {
                    return ImageLoadResult.Failure(
                        ImageLoadErrorKind.Timeout,
                        entry.Cts.IsCancellationRequested
                            ? "Download cancelled because no callers remain."
                            : "Overall download budget exceeded.");
                }

                if (attempt > 0)
                {
                    TimeSpan delay = CalculateBackoffDelay(request.RetryDelay, attempt);

                    try
                    {
                        await Task.Delay(delay, overallCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return ImageLoadResult.Failure(
                            ImageLoadErrorKind.Timeout,
                            entry.Cts.IsCancellationRequested
                                ? "Download cancelled because no callers remain."
                                : "Overall download budget exceeded during retry delay.");
                    }
                }

                await _downloadSlots.WaitAsync(overallCts.Token).ConfigureAwait(false);

                try
                {
                    lastResult = await DownloadOnceAsync(request, uri, perAttemptTimeout, overallCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OutOfMemoryException)
                {
                    _cache.TrimMemory(1.0);
                    return ImageLoadResult.Failure(ImageLoadErrorKind.Unknown, "The process is out of memory.");
                }
                finally
                {
                    _downloadSlots.Release();
                }

                if (lastResult.IsSuccess)
                {
                    TimeSpan? cacheDuration = request.CacheDuration ?? _options.DefaultCacheDuration;

                    if (request.CachePolicy != ImageCachePolicy.None)
                    {
                        await _cache.SetAsync(
                            cacheKey,
                            lastResult.ImageData!,
                            request.CachePolicy,
                            cacheDuration,
                            CancellationToken.None).ConfigureAwait(false);
                    }

                    return lastResult;
                }

                if (!IsRetryable(lastResult))
                {
                    _logger.LogDebug(
                        "Image download for {Url} failed with non-retryable error {ErrorKind} (HTTP {StatusCode}).",
                        request.Url,
                        lastResult.ErrorKind,
                        lastResult.HttpStatusCode);
                    return lastResult;
                }

                _logger.LogDebug(
                    "Image download for {Url} attempt {Attempt} failed with {ErrorKind}; retrying.",
                    request.Url,
                    attempt + 1,
                    lastResult.ErrorKind);
            }

            return lastResult;
        }
        catch (OperationCanceledException)
        {
            return ImageLoadResult.Failure(
                ImageLoadErrorKind.Timeout,
                entry.Cts.IsCancellationRequested
                    ? "Download cancelled because no callers remain."
                    : "Overall download budget exceeded.");
        }
        catch (OutOfMemoryException)
        {
            _cache.TrimMemory(1.0);
            return ImageLoadResult.Failure(ImageLoadErrorKind.Unknown, "The process is out of memory.");
        }
        finally
        {
            lock (entry.Gate)
            {
                entry.Removed = true;
            }

            RemoveIfSame(inFlightKey, entry);
            entry.Cts.Dispose();
        }
    }

    private TimeSpan CalculateBackoffDelay(TimeSpan baseDelay, int attempt)
    {
        double backoffMultiplier = Math.Pow(2, attempt - 1);
        double baseMs = Math.Max(0, baseDelay.TotalMilliseconds) * backoffMultiplier;
        double jitterMs;

        lock (_jitter)
        {
            jitterMs = _jitter.NextDouble() * Math.Max(1, baseMs * 0.2);
        }

        return TimeSpan.FromMilliseconds(baseMs + jitterMs);
    }

    private static TimeSpan CalculateOverallBudget(TimeSpan perAttemptTimeout, TimeSpan retryDelay, int maxAttempts)
    {
        double retryDelayTotalMs = 0;

        for (int attempt = 1; attempt < maxAttempts; attempt++)
        {
            retryDelayTotalMs += retryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1) * 1.2;
        }

        double totalMs = (perAttemptTimeout.TotalMilliseconds * maxAttempts) + retryDelayTotalMs;
        return TimeSpan.FromMilliseconds(Math.Max(perAttemptTimeout.TotalMilliseconds, totalMs));
    }

    private static string BuildInFlightKey(ImageLoadRequest request)
    {
        return string.Join(
            '|',
            request.Url,
            request.CachePolicy,
            request.Timeout,
            request.MaxImageSizeBytes,
            request.MaxRetryCount,
            request.EnableAutomaticRetry,
            request.RetryDelay);
    }

    private async Task<ImageLoadResult> DownloadOnceAsync(
        ImageLoadRequest request,
        Uri uri,
        TimeSpan perAttemptTimeout,
        CancellationToken overallToken)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(overallToken);
        timeoutCts.CancelAfter(perAttemptTimeout);

        long? maxBytes = request.MaxImageSizeBytes ?? _options.DefaultMaxImageSizeBytes;

        try
        {
            using HttpResponseMessage response = await GetHttpClient()
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                int statusCode = (int)response.StatusCode;
                return ImageLoadResult.Failure(
                    ImageLoadErrorKind.HttpError,
                    $"HTTP {statusCode}",
                    statusCode);
            }

            if (maxBytes is long maxBytesFromHeader &&
                response.Content.Headers.ContentLength is long contentLength &&
                contentLength > maxBytesFromHeader)
            {
                return ImageLoadResult.Failure(ImageLoadErrorKind.TooLarge);
            }

            byte[]? data = await ReadBoundedAsync(response, maxBytes, timeoutCts.Token).ConfigureAwait(false);

            if (data is null)
            {
                return ImageLoadResult.Failure(ImageLoadErrorKind.TooLarge);
            }

            if (!ImageSignature.IsRecognizedImage(data))
            {
                return ImageLoadResult.Failure(ImageLoadErrorKind.CorruptedData);
            }

            return ImageLoadResult.Success(data);
        }
        catch (OutOfMemoryException)
        {
            _cache.TrimMemory(1.0);
            return ImageLoadResult.Failure(ImageLoadErrorKind.Unknown, "The process is out of memory.");
        }
        catch (OperationCanceledException) when (overallToken.IsCancellationRequested)
        {
            return ImageLoadResult.Failure(ImageLoadErrorKind.Timeout, "Overall download budget exceeded.");
        }
        catch (OperationCanceledException)
        {
            return ImageLoadResult.Failure(ImageLoadErrorKind.Timeout);
        }
        catch (HttpRequestException ex)
        {
            ImageLoadErrorKind kind = ex.InnerException is AuthenticationException
                ? ImageLoadErrorKind.SslError
                : ImageLoadErrorKind.NetworkError;

            return ImageLoadResult.Failure(kind, ex.Message);
        }
        catch (IOException ex)
        {
            return ImageLoadResult.Failure(ImageLoadErrorKind.NetworkError, ex.Message);
        }
    }

    private static async Task<byte[]?> ReadBoundedAsync(
        HttpResponseMessage response,
        long? maxBytes,
        CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += bytesRead;

            if (maxBytes.HasValue && totalRead > maxBytes.Value)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static bool TryValidateUri(string url, out Uri uri)
    {
        bool isValid = Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUri) &&
            (parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps);

        uri = parsedUri ?? new Uri("about:invalid");
        return isValid;
    }

    private static bool IsRetryable(ImageLoadResult result)
    {
        return result.ErrorKind switch
        {
            ImageLoadErrorKind.Timeout => true,
            ImageLoadErrorKind.NetworkError => true,
            ImageLoadErrorKind.HttpError => IsTransientHttpStatus(result.HttpStatusCode),
            _ => false
        };
    }

    private static bool IsTransientHttpStatus(int? statusCode)
    {
        return statusCode is (int)HttpStatusCode.RequestTimeout
            or (int)HttpStatusCode.TooManyRequests
            or >= 500 and < 600;
    }

    private sealed class InFlightDownload
    {
        public object Gate { get; } = new();

        public CancellationTokenSource Cts { get; } = new();

        public int WaiterCount;

        public Task<ImageLoadResult>? Task;

        public bool Removed;
    }
}
