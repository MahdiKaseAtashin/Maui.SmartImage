using Xunit;
using System.Net;
using System.Security.Authentication;
using Maui.SmartImage.Services;
using FluentAssertions;
using NSubstitute;

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
