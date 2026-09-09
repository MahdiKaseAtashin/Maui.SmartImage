using Xunit;
using Maui.SmartImage.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace Maui.SmartImage.Tests;

public class ImageCacheTests : IDisposable
{
    private readonly string _diskCacheDirectory;
    private readonly IMemoryCache _memoryCache;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ImageCache _cache;

    public ImageCacheTests()
    {
        _diskCacheDirectory = Path.Combine(Path.GetTempPath(), "smart-image-cache-tests-" + Guid.NewGuid());
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _cache = new ImageCache(_memoryCache, _diskCacheDirectory, _timeProvider);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();

        if (Directory.Exists(_diskCacheDirectory))
        {
            Directory.Delete(_diskCacheDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task TryGetAsync_WithMemoryPolicyAfterSet_ReturnsCachedData()
    {
        byte[] data = [1, 2, 3];

        await _cache.SetAsync("key", data, ImageCachePolicy.Memory, null, CancellationToken.None);
        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Memory, CancellationToken.None);

        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task TryGetAsync_WithMemoryPolicyWithoutSet_ReturnsNull()
    {
        byte[]? result = await _cache.TryGetAsync("missing-key", ImageCachePolicy.Memory, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetAsync_WithDiskPolicyAfterSet_ReturnsCachedDataFromDisk()
    {
        byte[] data = [4, 5, 6];

        await _cache.SetAsync("key", data, ImageCachePolicy.Disk, null, CancellationToken.None);
        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeEquivalentTo(data);
        Directory.GetFiles(_diskCacheDirectory, "*.bin").Should().HaveCount(1);
    }

    [Fact]
    public async Task TryGetAsync_WithDiskPolicyWithoutSet_ReturnsNull()
    {
        byte[]? result = await _cache.TryGetAsync("missing-key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithMemoryAndDiskPolicy_WritesToBothTiers()
    {
        byte[] data = [7, 8, 9];

        await _cache.SetAsync("key", data, ImageCachePolicy.MemoryAndDisk, null, CancellationToken.None);

        (await _cache.TryGetAsync("key", ImageCachePolicy.Memory, CancellationToken.None)).Should().BeEquivalentTo(data);
        (await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None)).Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task TryGetAsync_WithMemoryAndDiskPolicy_PromotesDiskHitIntoMemory()
    {
        byte[] data = [10, 11, 12];

        // Populate only the disk tier directly through the cache's disk-only policy.
        await _cache.SetAsync("key", data, ImageCachePolicy.Disk, null, CancellationToken.None);

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.MemoryAndDisk, CancellationToken.None);
        result.Should().BeEquivalentTo(data);

        // A subsequent memory-only lookup should now hit, proving the disk hit was promoted.
        byte[]? memoryOnlyResult = await _cache.TryGetAsync("key", ImageCachePolicy.Memory, CancellationToken.None);
        memoryOnlyResult.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SetAsync_WithNonePolicy_DoesNotStoreAnywhere()
    {
        byte[] data = [13, 14, 15];

        await _cache.SetAsync("key", data, ImageCachePolicy.None, null, CancellationToken.None);

        (await _cache.TryGetAsync("key", ImageCachePolicy.MemoryAndDisk, CancellationToken.None)).Should().BeNull();
        Directory.Exists(_diskCacheDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task TryGetAsync_WithNonePolicy_AlwaysReturnsNullEvenIfPreviouslyCached()
    {
        byte[] data = [16, 17, 18];
        await _cache.SetAsync("key", data, ImageCachePolicy.MemoryAndDisk, null, CancellationToken.None);

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.None, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetAsync_AfterDiskEntryExpires_ReturnsNullAndDeletesFiles()
    {
        byte[] data = [19, 20, 21];

        await _cache.SetAsync("key", data, ImageCachePolicy.Disk, TimeSpan.FromMinutes(1), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeNull();
        Directory.GetFiles(_diskCacheDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task TryGetAsync_BeforeDiskEntryExpires_StillReturnsData()
    {
        byte[] data = [22, 23, 24];

        await _cache.SetAsync("key", data, ImageCachePolicy.Disk, TimeSpan.FromMinutes(10), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SetAsync_WithDiskPolicyAndNoDuration_NeverExpires()
    {
        byte[] data = [25, 26, 27];

        await _cache.SetAsync("key", data, ImageCachePolicy.Disk, null, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromDays(365));

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task SetAndTryGet_WithKeyContainingUnsafeFilesystemCharacters_RoundTripsSafely()
    {
        const string unsafeKey = "https://example.com/a b?x=1&y=<>:\"|?*é.jpg";
        byte[] data = [28, 29, 30];

        await _cache.SetAsync(unsafeKey, data, ImageCachePolicy.Disk, null, CancellationToken.None);
        byte[]? result = await _cache.TryGetAsync(unsafeKey, ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeEquivalentTo(data);

        string[] files = Directory.GetFiles(_diskCacheDirectory, "*.bin");
        files.Should().HaveCount(1);
        Path.GetFileNameWithoutExtension(files[0]).Should().MatchRegex("^[0-9a-f]+$");
    }

    private sealed class FakeTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtcNow;

        public void Advance(TimeSpan by)
        {
            _utcNow = _utcNow.Add(by);
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }
}
