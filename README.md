# SmartImage.Maui

<p align="center">
  <img src="https://raw.githubusercontent.com/MahdiKaseAtashin/Maui.SmartImage/main/images/logo.png" alt="SmartImage.Maui logo" width="160" />
</p>

[![NuGet](https://img.shields.io/nuget/v/SmartImage.Maui.svg)](https://www.nuget.org/packages/SmartImage.Maui)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/MahdiKaseAtashin/Maui.SmartImage/blob/main/LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4.svg)](https://dotnet.microsoft.com/)
[![MAUI](https://img.shields.io/badge/MAUI-10-0EA5E9.svg)](https://learn.microsoft.com/dotnet/maui/)

A cache-aware, retryable image control for **.NET MAUI 10**.

`SmartImage` detects whether a source is a local MAUI resource, a local file, or a remote HTTP(S) URL. Local images load through MAUI with no network. Remote images use a two-tier memory + disk cache, retry/backoff, request deduplication, size/timeout limits, and a Loading / Loaded / Failed UI — without blocking the UI thread.

<p align="center">
  <img src="https://raw.githubusercontent.com/MahdiKaseAtashin/Maui.SmartImage/main/images/caching-architecture.jpg" alt="SmartImage.Maui caching architecture" width="900" />
</p>

<p align="center"><em>Cache hit → serve from memory/disk. Cache miss → download, validate, save, then display.</em></p>

## Features

- Automatic **local vs remote** source detection
- **Memory + disk** caching with size budgets and TTL
- **Request deduplication** (concurrent loads of the same URL share one download)
- **Retry** with exponential backoff + jitter (408 / 429 / 5xx, timeouts, network errors)
- Skeleton shimmer, fade-in, placeholder / error images, retry overlay
- Configurable via `UseSmartImage(options => …)`

## Install

```sh
dotnet add package SmartImage.Maui
```

```csharp
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseSmartImage(); // required for remote sources
```

## Quick start

```xml
<ContentPage xmlns:controls="clr-namespace:Maui.SmartImage.Controls;assembly=Maui.SmartImage">
    <controls:SmartImage Source="logo.png" />
</ContentPage>
```

```xml
<controls:SmartImage
    Source="{Binding ImageUrl}"
    Placeholder="placeholder.png"
    ErrorImage="error.png"
    CachePolicy="MemoryAndDisk"
    CacheDuration="01:00:00"
    Timeout="00:00:10"
    EnableFadeAnimation="True"
    KeepPreviousImageWhileLoading="True" />
```

## How it works

1. Classify `Source` → empty / local / remote  
2. **Local** → bind to MAUI `Image` (no network)  
3. **Remote** → `IImageLoader` → memory → disk → HTTP download  
4. Validate image bytes → cache → show (optional fade)

Without `UseSmartImage()`, a remote `Source` sets `State = Failed` with a clear error.

## Common properties

| Property | Default | Notes |
|---|---|---|
| `Source` | `null` | Resource name, file path, or `http(s)` URL |
| `Placeholder` / `ErrorImage` | `null` | Shown while loading / on failure |
| `CachePolicy` | `MemoryAndDisk` | `None`, `Memory`, `Disk`, `MemoryAndDisk` |
| `CacheDuration` | options default (7 days) | Per-control override |
| `Timeout` | options default (15s) | Per-attempt |
| `MaxImageSizeBytes` | options default (15 MB) | Aborts oversized downloads |
| `EnableAutomaticRetry` | `true` | Transient failures only |
| `EnableFadeAnimation` | `true` | Fade when a remote image loads |
| `State` | `Idle` | `Idle` \| `Loading` \| `Loaded` \| `Failed` |
| `RetryCommand` | — | Built-in overlay + bindable command |

## Global options

```csharp
builder.UseSmartImage(options =>
{
    options.DefaultTimeout = TimeSpan.FromSeconds(10);
    options.DefaultCacheDuration = TimeSpan.FromDays(3);
    options.DefaultMaxImageSizeBytes = 10 * 1024 * 1024;
    options.MemoryCacheSizeLimitBytes = 32 * 1024 * 1024;
    options.MaxDiskCacheSizeBytes = 128 * 1024 * 1024;
    options.DiskCacheDirectory = Path.Combine(FileSystem.CacheDirectory, "my_app_image_cache");
    options.ConfigureHttpClient = client =>
        client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
});
```

Clear cache at runtime:

```csharp
var cache = services.GetRequiredService<IImageCache>();
await cache.ClearAsync(CancellationToken.None);
```

## Requirements

- .NET 10
- .NET MAUI 10

## Links

- Source: [github.com/MahdiKaseAtashin/Maui.SmartImage](https://github.com/MahdiKaseAtashin/Maui.SmartImage)
- Changelog: [CHANGELOG.md](https://github.com/MahdiKaseAtashin/Maui.SmartImage/blob/main/CHANGELOG.md)
- License: [MIT](https://github.com/MahdiKaseAtashin/Maui.SmartImage/blob/main/LICENSE)
