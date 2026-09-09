using System.Collections.Concurrent;
using System.Security.Authentication;
using Maui.SmartImage;

namespace Maui.SmartImage.Services;

public sealed class ImageLoader : IImageLoader
{
    private readonly HttpClient _httpClient;
    private readonly IImageCache _cache;
    private readonly SmartImageOptions _options;
    private readonly ConcurrentDictionary<string, Lazy<Task<ImageLoadResult>>> _inFlightDownloads = new();

    public ImageLoader(HttpClient httpClient, IImageCache cache, SmartImageOptions options)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options;
    }

    public async Task<ImageLoadResult> LoadAsync(ImageLoadRequest request, CancellationToken cancellationToken)
    {
        if (!TryValidateUri(request.Url, out Uri? uri))
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

        // The shared download is intentionally NOT tied to any single caller's CancellationToken:
        // multiple SmartImage instances may be awaiting the same URL, and one caller cancelling
        // (e.g. its Source changed) must not abort the download for the others. Each caller instead
        // stops *waiting* on its own token via WaitAsync below, while the shared download keeps running.
        Lazy<Task<ImageLoadResult>> lazyDownload = _inFlightDownloads.GetOrAdd(
            cacheKey,
            _ => new Lazy<Task<ImageLoadResult>>(
                () => DownloadWithRetryAsync(request, uri, cacheKey),
                LazyThreadSafetyMode.ExecutionAndPublication));

        return await lazyDownload.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<ImageLoadResult> DownloadWithRetryAsync(ImageLoadRequest request, Uri uri, string cacheKey)
    {
        try
        {
            int maxAttempts = request.EnableAutomaticRetry ? request.MaxRetryCount + 1 : 1;
            ImageLoadResult lastResult = ImageLoadResult.Failure(ImageLoadErrorKind.Unknown);

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (attempt > 0)
                {
                    double backoffMultiplier = Math.Pow(2, attempt - 1);
                    TimeSpan delay = TimeSpan.FromMilliseconds(request.RetryDelay.TotalMilliseconds * backoffMultiplier);
                    await Task.Delay(delay).ConfigureAwait(false);
                }

                lastResult = await DownloadOnceAsync(request, uri).ConfigureAwait(false);

                if (lastResult.IsSuccess)
                {
                    if (request.CachePolicy != ImageCachePolicy.None)
                    {
                        await _cache.SetAsync(
                            cacheKey,
                            lastResult.ImageData!,
                            request.CachePolicy,
                            request.CacheDuration,
                            CancellationToken.None).ConfigureAwait(false);
                    }

                    return lastResult;
                }

                if (!IsRetryable(lastResult.ErrorKind))
                {
                    return lastResult;
                }
            }

            return lastResult;
        }
        finally
        {
            _inFlightDownloads.TryRemove(cacheKey, out _);
        }
    }

    private async Task<ImageLoadResult> DownloadOnceAsync(ImageLoadRequest request, Uri uri)
    {
        using CancellationTokenSource timeoutCts = new(request.Timeout ?? _options.DefaultTimeout);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return ImageLoadResult.Failure(ImageLoadErrorKind.HttpError, $"HTTP {(int)response.StatusCode}");
            }

            if (request.MaxImageSizeBytes is long maxBytesFromHeader &&
                response.Content.Headers.ContentLength is long contentLength &&
                contentLength > maxBytesFromHeader)
            {
                return ImageLoadResult.Failure(ImageLoadErrorKind.TooLarge);
            }

            byte[]? data = await ReadBoundedAsync(response, request.MaxImageSizeBytes, timeoutCts.Token).ConfigureAwait(false);

            if (data is null)
            {
                return ImageLoadResult.Failure(ImageLoadErrorKind.TooLarge);
            }

            if (!IsRecognizedImage(data))
            {
                return ImageLoadResult.Failure(ImageLoadErrorKind.CorruptedData);
            }

            return ImageLoadResult.Success(data);
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

    private static bool IsRetryable(ImageLoadErrorKind? kind)
    {
        return kind is ImageLoadErrorKind.Timeout
            or ImageLoadErrorKind.NetworkError
            or ImageLoadErrorKind.HttpError
            or ImageLoadErrorKind.SslError;
    }

    private static bool IsRecognizedImage(byte[] data)
    {
        if (data.Length < 4)
        {
            return false;
        }

        if (data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
        {
            return true; // PNG
        }

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return true; // JPEG
        }

        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
        {
            return true; // GIF87a / GIF89a
        }

        if (data[0] == 0x42 && data[1] == 0x4D)
        {
            return true; // BMP
        }

        if (data.Length >= 12 &&
            data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
        {
            return true; // WebP (RIFF....WEBP)
        }

        return false;
    }
}
