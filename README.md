# Maui.SmartImage

A production-ready, cache-aware, retryable image control for .NET MAUI.

`SmartImage` automatically detects whether a source is a local MAUI resource, a local file, or a remote HTTP(S) URL. Local images load directly through MAUI with no network involved. Remote images are loaded through a small, testable pipeline that adds two-tier (memory + disk) caching, automatic retry with exponential backoff, request deduplication (concurrent requests for the same URL share one download), bounded timeouts, and a built-in Loading/Loaded/Failed state with a tappable retry affordance — all without ever risking a hang on the UI thread.

## Install

```sh
dotnet add package Maui.SmartImage
```

In `MauiProgram.cs`:

```csharp
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseSmartImage(); // registers IImageCache + IImageLoader
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

## Architecture

```
SmartImage (ContentView)
    ↓ resolves IImageLoader via Handler.MauiContext.Services
IImageLoader
    ↓ classifies Source: Empty / Local / Remote — Local bypasses the loader entirely
    ↓ Remote: cache lookup → dedup in-flight requests → retry w/ exponential backoff → timeout/size/signature checks
IImageCache
    ├── Memory tier — Microsoft.Extensions.Caching.Memory
    └── Disk tier   — FileSystem.CacheDirectory, SHA-256 keys, lazy sidecar-timestamp expiry
```

`IImageLoader` and `IImageCache` are registered as regular DI services (`UseSmartImage`), so they can be swapped or wrapped by the consuming app if needed.

## `SmartImage` reference

| Property | Type | Default | Notes |
|---|---|---|---|
| `Source` | `string?` | `null` | Local resource name, local file path, or `http(s)` URL. |
| `Placeholder` | `ImageSource?` | `null` | Shown while idle/loading (unless `KeepPreviousImageWhileLoading`) and as a fallback on failure if `ErrorImage` isn't set. |
| `ErrorImage` | `ImageSource?` | `null` | Shown on failure, in place of `Placeholder`. |
| `KeepPreviousImageWhileLoading` | `bool` | `false` | If `true`, the previously loaded image stays visible while a new load is in flight. |
| `CachePolicy` | `ImageCachePolicy` | `MemoryAndDisk` | `None` \| `Memory` \| `Disk` \| `MemoryAndDisk`. |
| `CacheDuration` | `TimeSpan?` | `null` | `null` = cache entry never expires (until evicted). |
| `Timeout` | `TimeSpan?` | `null` | Falls back to `SmartImageOptions.DefaultTimeout` (15s). |
| `MaxImageSizeBytes` | `long?` | `null` | Downloads exceeding this are aborted and treated as a (non-retried) failure. |
| `MaxRetryCount` | `int` | `2` | Automatic retries after the first attempt. |
| `EnableAutomaticRetry` | `bool` | `true` | |
| `RetryDelay` | `TimeSpan` | `1s` | Base delay; actual delay is `RetryDelay * 2^attempt` (exponential backoff). |
| `EnableFadeAnimation` | `bool` | `true` | Fades out/in when swapping to a newly loaded image. |
| `Aspect` | `Aspect` | `AspectFill` | Passed through to the inner `Image`. |
| `RetryButtonText` | `string` | `"Retry"` | Text of the tappable retry overlay shown on failure. |
| `RetryOverlayBackgroundColor` | `Color` | `#E5E7EB` | |
| `RetryButtonTextColor` | `Color` | `#374151` | |
| `RetryButtonFontSize` | `double` | `12` | |
| `SkeletonColor` | `Color` | `#E5E7EB` | Base color of the shimmering skeleton placeholder shown while loading. |
| `SkeletonHighlightColor` | `Color` | `#F9FAFB` | Color of the highlight bar that sweeps across the skeleton. |
| `State` (read-only) | `SmartImageState` | `Idle` | `Idle` \| `Loading` \| `Loaded` \| `Failed`. |
| `Error` (read-only) | `string?` | `null` | Set when `State == Failed`. |
| `RetryCommand` (read-only) | `ICommand` | | Also wired to the built-in retry overlay; bind your own retry UI to it if you don't use the default overlay. |

## `SmartImageOptions`

Configure globally via `builder.UseSmartImage(options => { ... })`:

```csharp
builder.UseSmartImage(options =>
{
    options.DefaultTimeout = TimeSpan.FromSeconds(10);
    options.DiskCacheDirectory = Path.Combine(FileSystem.CacheDirectory, "my_app_image_cache");
    options.PrimaryHttpMessageHandlerFactory = () => new HttpClientHandler
    {
        // e.g. dev-only certificate trust override
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    options.ConfigureHttpClient = client => client.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
});
```

## Error handling

Remote loads never throw past the `IImageLoader.LoadAsync` boundary and never leave the UI thread blocked indefinitely. Every failure mode — HTTP errors, timeouts, DNS/connection failures, SSL/TLS errors, corrupted or non-image response bodies, and oversized responses — resolves to a typed `ImageLoadErrorKind` and drives `SmartImage.State = Failed` with a retry affordance, instead of crashing the app or hanging.

## License

MIT — see [LICENSE](LICENSE).
