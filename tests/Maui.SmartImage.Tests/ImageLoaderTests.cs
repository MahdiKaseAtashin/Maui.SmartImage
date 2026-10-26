using System.Net;
using System.Security.Authentication;
using FluentAssertions;
using Maui.SmartImage;
using Maui.SmartImage.Services;
using NSubstitute;
using Xunit;

namespace Maui.SmartImage.Tests;

public class ImageLoaderTests
{
    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00
    ];

    private const string ImageUrl = "https://example.com/cover.jpg";

    private readonly IImageCache _cache = Substitute.For<IImageCache>();

    public ImageLoaderTests()
    {
        _cache.TryGetAsync(Arg.Any<string>(), Arg.Any<ImageCachePolicy>(), Arg.Any<CancellationToken>())
            .Returns((byte[]?)null);
    }

    [Fact]
    public async Task LoadAsync_WithInvalidUrl_ReturnsFailureWithoutTouchingHandlerOrCache()
    {
        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse(ValidPngBytes)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = "not-a-url" };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ImageLoadErrorKind.InvalidUrl);
        handler.CallCount.Should().Be(0);
        await _cache.DidNotReceive().TryGetAsync(Arg.Any<string>(), Arg.Any<ImageCachePolicy>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_WithFtpUrl_ReturnsFailureInvalidUrl()
    {
        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse(ValidPngBytes)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = "ftp://example.com/cover.jpg" };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.InvalidUrl);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_WithCacheHit_ReturnsSuccessWithoutTouchingHandler()
    {
        byte[] cachedBytes = [1, 2, 3];
        _cache.TryGetAsync(ImageUrl, Arg.Any<ImageCachePolicy>(), Arg.Any<CancellationToken>())
            .Returns(cachedBytes);

        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse(ValidPngBytes)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ImageData.Should().BeEquivalentTo(cachedBytes);
        handler.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_WithCacheMiss_DownloadsAndStoresInCache()
    {
        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse(ValidPngBytes)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, CachePolicy = ImageCachePolicy.MemoryAndDisk };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.ImageData.Should().BeEquivalentTo(ValidPngBytes);
        await _cache.Received(1).SetAsync(
            ImageUrl,
            Arg.Is<byte[]>(b => b.SequenceEqual(ValidPngBytes)),
            ImageCachePolicy.MemoryAndDisk,
            Arg.Any<TimeSpan?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_WithCachePolicyNone_NeverCallsCache()
    {
        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse(ValidPngBytes)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, CachePolicy = ImageCachePolicy.None };

        await loader.LoadAsync(request, CancellationToken.None);

        await _cache.DidNotReceive().TryGetAsync(Arg.Any<string>(), Arg.Any<ImageCachePolicy>(), Arg.Any<CancellationToken>());
        await _cache.DidNotReceive().SetAsync(
            Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<ImageCachePolicy>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_WithHttpErrorStatus_ReturnsFailureHttpError()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, EnableAutomaticRetry = false };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ImageLoadErrorKind.HttpError);
    }

    [Fact]
    public async Task LoadAsync_WhenRequestExceedsTimeout_ReturnsFailureTimeout()
    {
        FakeHttpMessageHandler handler = new(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return SuccessResponse(ValidPngBytes);
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new()
        {
            Url = ImageUrl,
            Timeout = TimeSpan.FromMilliseconds(50),
            EnableAutomaticRetry = false
        };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ImageLoadErrorKind.Timeout);
    }

    [Fact]
    public async Task LoadAsync_WhenHandlerThrowsSslAuthenticationException_ReturnsFailureSslError()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            throw new HttpRequestException("SSL failure", new AuthenticationException("bad cert")));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, EnableAutomaticRetry = false };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.SslError);
    }

    [Fact]
    public async Task LoadAsync_WhenHandlerThrowsGenericHttpRequestException_ReturnsFailureNetworkError()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            throw new HttpRequestException("DNS failure"));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, EnableAutomaticRetry = false };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.NetworkError);
    }

    [Fact]
    public async Task LoadAsync_WithNonImageBytes_ReturnsFailureCorruptedData()
    {
        byte[] corruptedBytes = "this is not an image"u8.ToArray();
        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse(corruptedBytes)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, EnableAutomaticRetry = false };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.CorruptedData);
    }

    [Fact]
    public async Task LoadAsync_CorruptedData_IsNotRetried()
    {
        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse("not-an-image"u8.ToArray())));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, EnableAutomaticRetry = true, MaxRetryCount = 3, RetryDelay = TimeSpan.FromMilliseconds(1) };

        await loader.LoadAsync(request, CancellationToken.None);

        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WhenContentLengthExceedsMaxImageSizeBytes_ReturnsFailureTooLarge()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = SuccessResponse(ValidPngBytes);
            response.Content.Headers.ContentLength = 10_000;
            return Task.FromResult(response);
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, MaxImageSizeBytes = 100, EnableAutomaticRetry = false };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.TooLarge);
    }

    [Fact]
    public async Task LoadAsync_WhenActualBodyExceedsMaxImageSizeBytesWithoutContentLength_ReturnsFailureTooLarge()
    {
        byte[] largeBytes = new byte[200];
        Array.Copy(ValidPngBytes, largeBytes, ValidPngBytes.Length);

        FakeHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(largeBytes))
            };
            return Task.FromResult(response);
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, MaxImageSizeBytes = 50, EnableAutomaticRetry = false };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.TooLarge);
    }

    [Fact]
    public async Task LoadAsync_WithAutomaticRetry_SucceedsAfterTransientFailures()
    {
        int attempt = 0;
        FakeHttpMessageHandler handler = new((_, _) =>
        {
            attempt++;
            return Task.FromResult(attempt <= 2
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : SuccessResponse(ValidPngBytes));
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new()
        {
            Url = ImageUrl,
            EnableAutomaticRetry = true,
            MaxRetryCount = 3,
            RetryDelay = TimeSpan.FromMilliseconds(1)
        };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task LoadAsync_WithAutomaticRetryDisabled_OnlyAttemptsOnce()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, EnableAutomaticRetry = false };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WithHttp404_DoesNotRetry()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new()
        {
            Url = ImageUrl,
            EnableAutomaticRetry = true,
            MaxRetryCount = 3,
            RetryDelay = TimeSpan.FromMilliseconds(1)
        };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.HttpError);
        result.HttpStatusCode.Should().Be(404);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WithHttp503_IsRetried()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new()
        {
            Url = ImageUrl,
            EnableAutomaticRetry = true,
            MaxRetryCount = 2,
            RetryDelay = TimeSpan.FromMilliseconds(1)
        };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.HttpError);
        result.HttpStatusCode.Should().Be(503);
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task LoadAsync_WithSslError_DoesNotRetry()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            throw new HttpRequestException("SSL failure", new AuthenticationException("bad cert")));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new()
        {
            Url = ImageUrl,
            EnableAutomaticRetry = true,
            MaxRetryCount = 3,
            RetryDelay = TimeSpan.FromMilliseconds(1)
        };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.SslError);
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WhenMaxImageSizeBytesNull_AppliesOptionsDefault()
    {
        byte[] largeBytes = new byte[200];
        Array.Copy(ValidPngBytes, largeBytes, ValidPngBytes.Length);

        FakeHttpMessageHandler handler = new((_, _) =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(largeBytes))
            };
            return Task.FromResult(response);
        });

        HttpClient httpClient = new(handler);
        ImageLoader loader = new(
            httpClient,
            _cache,
            new SmartImageOptions { DefaultMaxImageSizeBytes = 50 });

        ImageLoadResult result = await loader.LoadAsync(
            new ImageLoadRequest { Url = ImageUrl, MaxImageSizeBytes = null, EnableAutomaticRetry = false },
            CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.TooLarge);
    }

    [Fact]
    public async Task LoadAsync_AfterExhaustingRetries_ReturnsLastFailure()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new()
        {
            Url = ImageUrl,
            EnableAutomaticRetry = true,
            MaxRetryCount = 2,
            RetryDelay = TimeSpan.FromMilliseconds(1)
        };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorKind.Should().Be(ImageLoadErrorKind.HttpError);
        handler.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task LoadAsync_WithConcurrentRequestsForSameUrl_DownloadsOnlyOnce()
    {
        TaskCompletionSource gate = new();
        FakeHttpMessageHandler handler = new(async (_, ct) =>
        {
            await gate.Task.WaitAsync(ct);
            return SuccessResponse(ValidPngBytes);
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl };

        Task<ImageLoadResult> first = loader.LoadAsync(request, CancellationToken.None);
        Task<ImageLoadResult> second = loader.LoadAsync(request, CancellationToken.None);

        await Task.Delay(50);
        gate.SetResult();

        ImageLoadResult[] results = await Task.WhenAll(first, second);

        handler.CallCount.Should().Be(1);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
        results[0].ImageData.Should().BeEquivalentTo(ValidPngBytes);
        results[1].ImageData.Should().BeEquivalentTo(ValidPngBytes);
    }

    [Fact]
    public async Task LoadAsync_WithConcurrentRequestsForSameUrlButDifferentSettings_DownloadsIndependently()
    {
        TaskCompletionSource gate = new();
        FakeHttpMessageHandler handler = new(async (_, ct) =>
        {
            await gate.Task.WaitAsync(ct);
            return SuccessResponse(ValidPngBytes);
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest smallCapRequest = new() { Url = ImageUrl, MaxImageSizeBytes = 1, EnableAutomaticRetry = false };
        ImageLoadRequest noCapRequest = new() { Url = ImageUrl, MaxImageSizeBytes = null, EnableAutomaticRetry = false };

        Task<ImageLoadResult> smallCapCall = loader.LoadAsync(smallCapRequest, CancellationToken.None);
        Task<ImageLoadResult> noCapCall = loader.LoadAsync(noCapRequest, CancellationToken.None);

        await Task.Delay(50);
        gate.SetResult();

        ImageLoadResult[] results = await Task.WhenAll(smallCapCall, noCapCall);

        handler.CallCount.Should().Be(2);
        results[0].ErrorKind.Should().Be(ImageLoadErrorKind.TooLarge);
        results[1].IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_WhenCallerCancelsWhileWaiting_ThrowsWithoutFailingTheSharedDownload()
    {
        TaskCompletionSource gate = new();
        FakeHttpMessageHandler handler = new(async (_, ct) =>
        {
            await gate.Task.WaitAsync(ct);
            return SuccessResponse(ValidPngBytes);
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl };

        using CancellationTokenSource callerCts = new();
        Task<ImageLoadResult> cancelledCall = loader.LoadAsync(request, callerCts.Token);
        Task<ImageLoadResult> patientCall = loader.LoadAsync(request, CancellationToken.None);

        callerCts.Cancel();

        await FluentActions.Awaiting(() => cancelledCall).Should().ThrowAsync<OperationCanceledException>();

        gate.SetResult();
        ImageLoadResult patientResult = await patientCall;

        patientResult.IsSuccess.Should().BeTrue();
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WhenLastWaiterCancels_AbortsTheDownload()
    {
        TaskCompletionSource started = new();
        TaskCompletionSource aborted = new();
        FakeHttpMessageHandler handler = new(async (_, ct) =>
        {
            started.TrySetResult();

            try
            {
                await Task.Delay(Timeout.Infinite, ct);
            }
            catch (OperationCanceledException)
            {
                aborted.TrySetResult();
                throw;
            }

            return SuccessResponse(ValidPngBytes);
        });
        ImageLoader loader = CreateLoader(handler);
        ImageLoadRequest request = new() { Url = ImageUrl, EnableAutomaticRetry = false };

        using CancellationTokenSource callerCts = new();
        Task<ImageLoadResult> load = loader.LoadAsync(request, callerCts.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        callerCts.Cancel();

        await FluentActions.Awaiting(() => load).Should().ThrowAsync<OperationCanceledException>();
        await aborted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WhenLastWaiterCancelsAfterDownloadCompleted_ReturnsSuccess()
    {
        TaskCompletionSource started = new();
        TaskCompletionSource cached = new();
        FakeHttpMessageHandler handler = new(async (_, _) =>
        {
            started.TrySetResult();
            return SuccessResponse(ValidPngBytes);
        });
        _cache.SetAsync(
                Arg.Any<string>(),
                Arg.Any<byte[]>(),
                Arg.Any<ImageCachePolicy>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cached.TrySetResult();
                return Task.CompletedTask;
            });

        ImageLoader loader = CreateLoader(handler);
        using CancellationTokenSource callerCts = new();
        Task<ImageLoadResult> load = loader.LoadAsync(
            new ImageLoadRequest { Url = ImageUrl, EnableAutomaticRetry = false },
            callerCts.Token);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cached.Task.WaitAsync(TimeSpan.FromSeconds(2));
        callerCts.Cancel();

        ImageLoadResult result = await load;
        result.IsSuccess.Should().BeTrue();
        result.ImageData.Should().BeEquivalentTo(ValidPngBytes);
    }

    [Fact]
    public async Task LoadAsync_WithHttpClientFactory_CreatesClientPerDownload()
    {
        FakeHttpMessageHandler handler = new((_, _) => Task.FromResult(SuccessResponse(ValidPngBytes)));
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler, disposeHandler: false));

        ImageLoader loader = new(factory, _cache, new SmartImageOptions());

        await loader.LoadAsync(
            new ImageLoadRequest { Url = "https://example.com/a.jpg", EnableAutomaticRetry = false },
            CancellationToken.None);
        await loader.LoadAsync(
            new ImageLoadRequest { Url = "https://example.com/b.jpg", EnableAutomaticRetry = false },
            CancellationToken.None);

        factory.Received(2).CreateClient(MauiAppBuilderExtensions.HttpClientName);
        handler.CallCount.Should().Be(2);
    }

    [Fact]
    public void MaxInFlightJoinAttempts_IsBounded()
    {
        ImageLoader.MaxInFlightJoinAttempts.Should().Be(16);
    }

    [Fact]
    public async Task LoadAsync_RespectsMaxConcurrentDownloads()
    {
        int inFlight = 0;
        int maxSeen = 0;
        TaskCompletionSource gate = new();
        FakeHttpMessageHandler handler = new(async (_, ct) =>
        {
            int now = Interlocked.Increment(ref inFlight);

            int snapshot;
            do
            {
                snapshot = maxSeen;
            }
            while (now > snapshot && Interlocked.CompareExchange(ref maxSeen, now, snapshot) != snapshot);

            try
            {
                await gate.Task.WaitAsync(ct);
                return SuccessResponse(ValidPngBytes);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        });

        HttpClient httpClient = new(handler);
        ImageLoader loader = new(
            httpClient,
            _cache,
            new SmartImageOptions { MaxConcurrentDownloads = 2 });

        Task<ImageLoadResult>[] loads = Enumerable.Range(0, 5)
            .Select(i => loader.LoadAsync(
                new ImageLoadRequest
                {
                    Url = $"https://example.com/{i}.jpg",
                    EnableAutomaticRetry = false
                },
                CancellationToken.None))
            .ToArray();

        await Task.Delay(150);
        maxSeen.Should().BeLessThanOrEqualTo(2);

        gate.SetResult();
        ImageLoadResult[] results = await Task.WhenAll(loads);
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    [Fact]
    public async Task LoadAsync_WhenDeviceConstrained_DoesNotRetry()
    {
        FakeHttpMessageHandler handler = new((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        FakeDeviceResourceMonitor monitor = new() { IsConstrained = true };
        HttpClient httpClient = new(handler);
        ImageLoader loader = new(
            httpClient,
            _cache,
            new SmartImageOptions(),
            logger: null,
            deviceResources: monitor);
        ImageLoadRequest request = new()
        {
            Url = ImageUrl,
            EnableAutomaticRetry = true,
            MaxRetryCount = 2,
            RetryDelay = TimeSpan.FromMilliseconds(1)
        };

        ImageLoadResult result = await loader.LoadAsync(request, CancellationToken.None);

        result.ErrorKind.Should().Be(ImageLoadErrorKind.HttpError);
        handler.CallCount.Should().Be(1);
    }

    private ImageLoader CreateLoader(HttpMessageHandler handler)
    {
        HttpClient httpClient = new(handler);
        return new ImageLoader(httpClient, _cache, new SmartImageOptions());
    }

    private static HttpResponseMessage SuccessResponse(byte[] body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
        };
    }

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => _callCount;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            return await handler(request, cancellationToken);
        }
    }
}
