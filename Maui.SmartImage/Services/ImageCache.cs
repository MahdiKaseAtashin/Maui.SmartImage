using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Maui.SmartImage.Services;

/// <summary>
/// Default <see cref="IImageCache"/> implementation using <see cref="IMemoryCache"/> and a disk directory.
/// </summary>
public sealed class ImageCache : IImageCache, IDisposable
{
    private const string DataFileExtension = ".bin";
    private const string ExpiryFileExtension = ".exp";
    private const string TempFileExtension = ".tmp";

    private readonly IMemoryCache _memoryCache;
    private readonly string _diskCacheDirectory;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _defaultEntryDuration;
    private readonly long _maxDiskCacheSizeBytes;
    private readonly bool _ownsMemoryCache;
    private readonly object _diskEvictionLock = new();
    private readonly Dictionary<string, DiskIndexEntry> _diskIndex = new(StringComparer.Ordinal);
    private long _trackedDiskSize;
    private bool _diskIndexInitialized;

    /// <summary>
    /// Creates a new image cache.
    /// </summary>
    /// <param name="memoryCache">Memory tier.</param>
    /// <param name="diskCacheDirectory">Disk tier root directory.</param>
    /// <param name="timeProvider">Clock used for expiry checks (injectable for tests).</param>
    /// <param name="defaultEntryDuration">Applied when <c>duration</c> is null on set, and when promoting disk hits with no expiry sidecar.</param>
    /// <param name="maxDiskCacheSizeBytes">Soft size budget for the disk tier; oldest files are evicted when exceeded.</param>
    /// <param name="ownsMemoryCache">When <see langword="true"/>, disposes <paramref name="memoryCache"/> with this instance.</param>
    public ImageCache(
        IMemoryCache memoryCache,
        string diskCacheDirectory,
        TimeProvider timeProvider,
        TimeSpan? defaultEntryDuration = null,
        long? maxDiskCacheSizeBytes = null,
        bool ownsMemoryCache = false)
    {
        _memoryCache = memoryCache;
        _diskCacheDirectory = diskCacheDirectory;
        _timeProvider = timeProvider;
        _defaultEntryDuration = defaultEntryDuration ?? TimeSpan.FromDays(7);
        _maxDiskCacheSizeBytes = maxDiskCacheSizeBytes ?? SmartImageOptions.DefaultMaxDiskCacheSizeBytes;
        _ownsMemoryCache = ownsMemoryCache;
    }

    /// <inheritdoc />
    public async Task<byte[]?> TryGetAsync(string key, ImageCachePolicy policy, CancellationToken cancellationToken)
    {
        if (policy == ImageCachePolicy.None)
        {
            return null;
        }

        string cacheKey = ComputeCacheKey(key);

        if (UsesMemory(policy) && _memoryCache.TryGetValue(cacheKey, out byte[]? memoryData) && memoryData is not null)
        {
            return memoryData;
        }

        if (!UsesDisk(policy))
        {
            return null;
        }

        (byte[]? diskData, TimeSpan? remainingTtl) = await TryReadFromDiskAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        if (diskData is not null && UsesMemory(policy))
        {
            SetMemoryEntry(cacheKey, diskData, remainingTtl ?? _defaultEntryDuration);
        }

        return diskData;
    }

    /// <inheritdoc />
    public async Task SetAsync(
        string key,
        byte[] data,
        ImageCachePolicy policy,
        TimeSpan? duration,
        CancellationToken cancellationToken)
    {
        if (policy == ImageCachePolicy.None)
        {
            return;
        }

        string cacheKey = ComputeCacheKey(key);
        TimeSpan effectiveDuration = duration ?? _defaultEntryDuration;

        if (UsesMemory(policy))
        {
            SetMemoryEntry(cacheKey, data, effectiveDuration);
        }

        if (UsesDisk(policy))
        {
            await TryWriteToDiskAsync(cacheKey, data, effectiveDuration, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string cacheKey = ComputeCacheKey(key);
        _memoryCache.Remove(cacheKey);
        DeleteDiskEntry(GetDataFilePath(cacheKey), GetExpiryFilePath(cacheKey));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TrimMemory(1.0);

        try
        {
            if (Directory.Exists(_diskCacheDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(_diskCacheDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TryDeleteFile(file);
                }
            }
        }
        catch (IOException)
        {
            // Best-effort clear.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort clear.
        }

        lock (_diskEvictionLock)
        {
            _diskIndex.Clear();
            _trackedDiskSize = 0;
            _diskIndexInitialized = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void TrimMemory(double percentage = 1.0)
    {
        if (_memoryCache is MemoryCache concreteMemoryCache)
        {
            concreteMemoryCache.Compact(Math.Clamp(percentage, 0, 1));
        }
    }

    private void SetMemoryEntry(string cacheKey, byte[] data, TimeSpan duration)
    {
        MemoryCacheEntryOptions options = new()
        {
            AbsoluteExpirationRelativeToNow = duration,
            Size = data.LongLength
        };

        try
        {
            _memoryCache.Set(cacheKey, data, options);
        }
        catch (OutOfMemoryException)
        {
            TrimMemory(1.0);
            _memoryCache.Set(cacheKey, data, options);
        }
    }

    private static bool UsesMemory(ImageCachePolicy policy)
    {
        return policy is ImageCachePolicy.Memory or ImageCachePolicy.MemoryAndDisk;
    }

    private static bool UsesDisk(ImageCachePolicy policy)
    {
        return policy is ImageCachePolicy.Disk or ImageCachePolicy.MemoryAndDisk;
    }

    private async Task<(byte[]? Data, TimeSpan? RemainingTtl)> TryReadFromDiskAsync(
        string cacheKey,
        CancellationToken cancellationToken)
    {
        try
        {
            string dataPath = GetDataFilePath(cacheKey);
            string expiryPath = GetExpiryFilePath(cacheKey);

            if (!File.Exists(dataPath))
            {
                return (null, null);
            }

            TimeSpan? remainingTtl = null;

            if (File.Exists(expiryPath))
            {
                string expiryText = await File.ReadAllTextAsync(expiryPath, cancellationToken).ConfigureAwait(false);

                if (DateTimeOffset.TryParse(
                        expiryText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out DateTimeOffset expiresAt))
                {
                    DateTimeOffset now = _timeProvider.GetUtcNow();

                    if (expiresAt <= now)
                    {
                        DeleteDiskEntry(dataPath, expiryPath);
                        return (null, null);
                    }

                    remainingTtl = expiresAt - now;
                }
            }

            byte[] data = await File.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);

            if (!ImageSignature.IsRecognizedImage(data))
            {
                DeleteDiskEntry(dataPath, expiryPath);
                return (null, null);
            }

            return (data, remainingTtl);
        }
        catch (IOException)
        {
            return (null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    private async Task TryWriteToDiskAsync(
        string cacheKey,
        byte[] data,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_diskCacheDirectory);

            string dataPath = GetDataFilePath(cacheKey);
            string expiryPath = GetExpiryFilePath(cacheKey);
            string tempPath = Path.Combine(
                _diskCacheDirectory,
                cacheKey + "." + Guid.NewGuid().ToString("N") + TempFileExtension);

            try
            {
                await File.WriteAllBytesAsync(tempPath, data, cancellationToken).ConfigureAwait(false);
                File.Move(tempPath, dataPath, overwrite: true);
            }
            finally
            {
                TryDeleteFile(tempPath);
            }

            DateTimeOffset expiresAt = _timeProvider.GetUtcNow().Add(duration);
            string expiryTempPath = Path.Combine(
                _diskCacheDirectory,
                cacheKey + "." + Guid.NewGuid().ToString("N") + ExpiryFileExtension + TempFileExtension);

            try
            {
                await File.WriteAllTextAsync(expiryTempPath, expiresAt.ToString("O"), cancellationToken).ConfigureAwait(false);
                File.Move(expiryTempPath, expiryPath, overwrite: true);
            }
            finally
            {
                TryDeleteFile(expiryTempPath);
            }

            TrackWrittenDiskEntry(cacheKey, dataPath);
            EvictUntilUnderBudget();
        }
        catch (IOException)
        {
            // Disk cache failures must never fail the overall image load.
        }
        catch (UnauthorizedAccessException)
        {
            // Disk cache failures must never fail the overall image load.
        }
    }

    private void TrackWrittenDiskEntry(string cacheKey, string dataPath)
    {
        FileInfo written = new(dataPath);
        long length = written.Exists ? written.Length : 0;
        DateTime lastWrite = written.Exists ? written.LastWriteTimeUtc : _timeProvider.GetUtcNow().UtcDateTime;

        lock (_diskEvictionLock)
        {
            EnsureDiskIndexLocked();

            if (_diskIndex.TryGetValue(cacheKey, out DiskIndexEntry? existing))
            {
                _trackedDiskSize -= existing.Length;
                existing.Length = length;
                existing.LastWriteTimeUtc = lastWrite;
                _trackedDiskSize += length;
            }
            else
            {
                _diskIndex[cacheKey] = new DiskIndexEntry
                {
                    CacheKey = cacheKey,
                    Length = length,
                    LastWriteTimeUtc = lastWrite
                };
                _trackedDiskSize += length;
            }

            if (_trackedDiskSize < 0)
            {
                _trackedDiskSize = 0;
            }
        }
    }

    private void EvictUntilUnderBudget()
    {
        lock (_diskEvictionLock)
        {
            try
            {
                EnsureDiskIndexLocked();

                if (_trackedDiskSize <= _maxDiskCacheSizeBytes)
                {
                    return;
                }

                List<DiskIndexEntry> oldestFirst = _diskIndex.Values
                    .OrderBy(entry => entry.LastWriteTimeUtc)
                    .ToList();

                foreach (DiskIndexEntry entry in oldestFirst)
                {
                    if (_trackedDiskSize <= _maxDiskCacheSizeBytes)
                    {
                        break;
                    }

                    TryDeleteFile(GetDataFilePath(entry.CacheKey));
                    TryDeleteFile(GetExpiryFilePath(entry.CacheKey));
                    RemoveFromIndexLocked(entry.CacheKey);
                }
            }
            catch (IOException)
            {
                // Best-effort eviction.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort eviction.
            }
        }
    }

    private void EnsureDiskIndexLocked()
    {
        if (_diskIndexInitialized)
        {
            return;
        }

        _diskIndex.Clear();
        _trackedDiskSize = 0;

        try
        {
            if (Directory.Exists(_diskCacheDirectory))
            {
                foreach (FileInfo info in Directory
                    .EnumerateFiles(_diskCacheDirectory, "*" + DataFileExtension)
                    .Select(path => new FileInfo(path))
                    .Where(file => file.Exists && IsDataFile(file.Name)))
                {
                    string cacheKey = Path.GetFileNameWithoutExtension(info.Name);
                    _diskIndex[cacheKey] = new DiskIndexEntry
                    {
                        CacheKey = cacheKey,
                        Length = info.Length,
                        LastWriteTimeUtc = info.LastWriteTimeUtc
                    };
                    _trackedDiskSize += info.Length;
                }
            }
        }
        catch (IOException)
        {
            // Index will rebuild on the next write.
        }
        catch (UnauthorizedAccessException)
        {
            // Index will rebuild on the next write.
        }

        _diskIndexInitialized = true;
    }

    private void RemoveFromIndexLocked(string cacheKey)
    {
        if (_diskIndexInitialized && _diskIndex.Remove(cacheKey, out DiskIndexEntry? entry))
        {
            _trackedDiskSize -= entry.Length;

            if (_trackedDiskSize < 0)
            {
                _trackedDiskSize = 0;
            }
        }
    }

    private static bool IsDataFile(string fileName)
    {
        // Real data files are "{sha256}.bin" (64 hex chars). Temp names contain extra dots.
        return fileName.EndsWith(DataFileExtension, StringComparison.OrdinalIgnoreCase)
            && fileName.Length == 64 + DataFileExtension.Length;
    }

    private void DeleteDiskEntry(string dataPath, string expiryPath)
    {
        TryDeleteFile(dataPath);
        TryDeleteFile(expiryPath);

        string cacheKey = Path.GetFileNameWithoutExtension(dataPath);
        lock (_diskEvictionLock)
        {
            RemoveFromIndexLocked(cacheKey);
        }
    }

    private sealed class DiskIndexEntry
    {
        public required string CacheKey { get; init; }

        public long Length { get; set; }

        public DateTime LastWriteTimeUtc { get; set; }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

    private string GetDataFilePath(string cacheKey)
    {
        return Path.Combine(_diskCacheDirectory, cacheKey + DataFileExtension);
    }

    private string GetExpiryFilePath(string cacheKey)
    {
        return Path.Combine(_diskCacheDirectory, cacheKey + ExpiryFileExtension);
    }

    private static string ComputeCacheKey(string key)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(hash);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsMemoryCache && _memoryCache is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
