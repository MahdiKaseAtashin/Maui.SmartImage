using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace Maui.SmartImage.Services;

public sealed class ImageCache : IImageCache
{
    private const string DataFileExtension = ".bin";
    private const string ExpiryFileExtension = ".exp";

    private readonly IMemoryCache _memoryCache;
    private readonly string _diskCacheDirectory;
    private readonly TimeProvider _timeProvider;

    public ImageCache(IMemoryCache memoryCache, string diskCacheDirectory, TimeProvider timeProvider)
    {
        _memoryCache = memoryCache;
        _diskCacheDirectory = diskCacheDirectory;
        _timeProvider = timeProvider;
    }

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

        byte[]? diskData = await TryReadFromDiskAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        if (diskData is not null && UsesMemory(policy))
        {
            _memoryCache.Set(cacheKey, diskData);
        }

        return diskData;
    }

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

        if (UsesMemory(policy))
        {
            if (duration.HasValue)
            {
                _memoryCache.Set(cacheKey, data, duration.Value);
            }
            else
            {
                _memoryCache.Set(cacheKey, data);
            }
        }

        if (UsesDisk(policy))
        {
            await TryWriteToDiskAsync(cacheKey, data, duration, cancellationToken).ConfigureAwait(false);
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

    private async Task<byte[]?> TryReadFromDiskAsync(string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            string dataPath = GetDataFilePath(cacheKey);
            string expiryPath = GetExpiryFilePath(cacheKey);

            if (!File.Exists(dataPath))
            {
                return null;
            }

            if (File.Exists(expiryPath))
            {
                string expiryText = await File.ReadAllTextAsync(expiryPath, cancellationToken).ConfigureAwait(false);

                if (DateTimeOffset.TryParse(
                        expiryText,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind,
                        out DateTimeOffset expiresAt) &&
                    expiresAt <= _timeProvider.GetUtcNow())
                {
                    DeleteDiskEntry(dataPath, expiryPath);
                    return null;
                }
            }

            return await File.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task TryWriteToDiskAsync(
        string cacheKey,
        byte[] data,
        TimeSpan? duration,
        CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(_diskCacheDirectory);

            string dataPath = GetDataFilePath(cacheKey);
            string expiryPath = GetExpiryFilePath(cacheKey);

            await File.WriteAllBytesAsync(dataPath, data, cancellationToken).ConfigureAwait(false);

            if (duration.HasValue)
            {
                DateTimeOffset expiresAt = _timeProvider.GetUtcNow().Add(duration.Value);
                await File.WriteAllTextAsync(expiryPath, expiresAt.ToString("O"), cancellationToken).ConfigureAwait(false);
            }
            else if (File.Exists(expiryPath))
            {
                File.Delete(expiryPath);
            }
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

    private static void DeleteDiskEntry(string dataPath, string expiryPath)
    {
        try
        {
            if (File.Exists(dataPath))
            {
                File.Delete(dataPath);
            }

            if (File.Exists(expiryPath))
            {
                File.Delete(expiryPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of an expired entry; a stale file left behind is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of an expired entry; a stale file left behind is harmless.
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
}
