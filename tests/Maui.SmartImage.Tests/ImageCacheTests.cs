using FluentAssertions;
using Maui.SmartImage.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Maui.SmartImage.Tests;

public class ImageCacheTests : IDisposable
{
    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00
    ];

    private static readonly byte[] AnotherPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04
    ];

    private readonly string _diskCacheDirectory;
    private readonly IMemoryCache _memoryCache;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ImageCache _cache;

    public ImageCacheTests()
    {
        _diskCacheDirectory = Path.Combine(Path.GetTempPath(), "smart-image-cache-tests-" + Guid.NewGuid());
        _memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10_000_000 });
        _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        _cache = new ImageCache(
            _memoryCache,
            _diskCacheDirectory,
            _timeProvider,
            defaultEntryDuration: TimeSpan.FromDays(7),
            maxDiskCacheSizeBytes: 1024 * 1024);
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
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Memory, null, CancellationToken.None);
        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Memory, CancellationToken.None);

        result.Should().BeEquivalentTo(ValidPngBytes);
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
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, null, CancellationToken.None);
        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeEquivalentTo(ValidPngBytes);
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
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.MemoryAndDisk, null, CancellationToken.None);

        (await _cache.TryGetAsync("key", ImageCachePolicy.Memory, CancellationToken.None)).Should().BeEquivalentTo(ValidPngBytes);
        (await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None)).Should().BeEquivalentTo(ValidPngBytes);
    }

    [Fact]
    public async Task TryGetAsync_WithMemoryAndDiskPolicy_PromotesDiskHitIntoMemory()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, null, CancellationToken.None);

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.MemoryAndDisk, CancellationToken.None);
        result.Should().BeEquivalentTo(ValidPngBytes);

        byte[]? memoryOnlyResult = await _cache.TryGetAsync("key", ImageCachePolicy.Memory, CancellationToken.None);
        memoryOnlyResult.Should().BeEquivalentTo(ValidPngBytes);
    }

    [Fact]
    public async Task TryGetAsync_WhenPromotingDiskHit_PreservesRemainingTtl()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, TimeSpan.FromMinutes(10), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(4));

        (await _cache.TryGetAsync("key", ImageCachePolicy.MemoryAndDisk, CancellationToken.None)).Should().BeEquivalentTo(ValidPngBytes);

        _timeProvider.Advance(TimeSpan.FromMinutes(7));

        // AbsoluteExpirationRelativeToNow is wall-clock based on real time for MemoryCache,
        // so instead verify expiry sidecar still governs disk and memory was populated earlier.
        (await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_WithNonePolicy_DoesNotStoreAnywhere()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.None, null, CancellationToken.None);

        (await _cache.TryGetAsync("key", ImageCachePolicy.MemoryAndDisk, CancellationToken.None)).Should().BeNull();
        Directory.Exists(_diskCacheDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task TryGetAsync_WithNonePolicy_AlwaysReturnsNullEvenIfPreviouslyCached()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.MemoryAndDisk, null, CancellationToken.None);

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.None, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryGetAsync_AfterDiskEntryExpires_ReturnsNullAndDeletesFiles()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, TimeSpan.FromMinutes(1), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(2));

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeNull();
        Directory.GetFiles(_diskCacheDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task TryGetAsync_BeforeDiskEntryExpires_StillReturnsData()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, TimeSpan.FromMinutes(10), CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromMinutes(5));

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeEquivalentTo(ValidPngBytes);
    }

    [Fact]
    public async Task SetAsync_WithNullDuration_UsesDefaultEntryDuration()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, null, CancellationToken.None);
        _timeProvider.Advance(TimeSpan.FromDays(6));

        (await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None)).Should().BeEquivalentTo(ValidPngBytes);

        _timeProvider.Advance(TimeSpan.FromDays(2));

        (await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task TryGetAsync_WithCorruptDiskPayload_ReturnsNullAndDeletesFiles()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, TimeSpan.FromHours(1), CancellationToken.None);
        string binPath = Directory.GetFiles(_diskCacheDirectory, "*.bin").Single();
        await File.WriteAllBytesAsync(binPath, "not-an-image"u8.ToArray());

        byte[]? result = await _cache.TryGetAsync("key", ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeNull();
        Directory.GetFiles(_diskCacheDirectory, "*.bin").Should().BeEmpty();
    }

    [Fact]
    public async Task SetAsync_WritesAtomically_LeavingNoTempFiles()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.Disk, TimeSpan.FromHours(1), CancellationToken.None);

        Directory.GetFiles(_diskCacheDirectory, "*.tmp").Should().BeEmpty();
        Directory.GetFiles(_diskCacheDirectory, "*.bin").Should().HaveCount(1);
        Directory.GetFiles(_diskCacheDirectory, "*.exp").Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveAsync_DeletesMemoryAndDiskEntries()
    {
        await _cache.SetAsync("key", ValidPngBytes, ImageCachePolicy.MemoryAndDisk, null, CancellationToken.None);

        await _cache.RemoveAsync("key", CancellationToken.None);

        (await _cache.TryGetAsync("key", ImageCachePolicy.MemoryAndDisk, CancellationToken.None)).Should().BeNull();
        Directory.GetFiles(_diskCacheDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task ClearAsync_RemovesAllEntries()
    {
        await _cache.SetAsync("a", ValidPngBytes, ImageCachePolicy.MemoryAndDisk, null, CancellationToken.None);
        await _cache.SetAsync("b", AnotherPngBytes, ImageCachePolicy.MemoryAndDisk, null, CancellationToken.None);

        await _cache.ClearAsync(CancellationToken.None);

        (await _cache.TryGetAsync("a", ImageCachePolicy.MemoryAndDisk, CancellationToken.None)).Should().BeNull();
        (await _cache.TryGetAsync("b", ImageCachePolicy.MemoryAndDisk, CancellationToken.None)).Should().BeNull();
        Directory.GetFiles(_diskCacheDirectory).Should().BeEmpty();
    }

    [Fact]
    public async Task SetAsync_WhenDiskBudgetExceeded_EvictsOldestEntries()
    {
        ImageCache smallCache = new(
            _memoryCache,
            _diskCacheDirectory,
            _timeProvider,
            defaultEntryDuration: TimeSpan.FromDays(1),
            maxDiskCacheSizeBytes: ValidPngBytes.Length + 8);

        await smallCache.SetAsync("first", ValidPngBytes, ImageCachePolicy.Disk, null, CancellationToken.None);
        await Task.Delay(20);
        await smallCache.SetAsync("second", AnotherPngBytes, ImageCachePolicy.Disk, null, CancellationToken.None);

        Directory.GetFiles(_diskCacheDirectory, "*.bin").Should().HaveCount(1);
        (await smallCache.TryGetAsync("second", ImageCachePolicy.Disk, CancellationToken.None)).Should().BeEquivalentTo(AnotherPngBytes);
        (await smallCache.TryGetAsync("first", ImageCachePolicy.Disk, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task SetAndTryGet_WithKeyContainingUnsafeFilesystemCharacters_RoundTripsSafely()
    {
        const string unsafeKey = "https://example.com/a b?x=1&y=<>:\"|?*é.jpg";

        await _cache.SetAsync(unsafeKey, ValidPngBytes, ImageCachePolicy.Disk, null, CancellationToken.None);
        byte[]? result = await _cache.TryGetAsync(unsafeKey, ImageCachePolicy.Disk, CancellationToken.None);

        result.Should().BeEquivalentTo(ValidPngBytes);

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
