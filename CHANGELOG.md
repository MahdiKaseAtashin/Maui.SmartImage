# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `SmartImageOptions.MaxConcurrentDownloads` (default 4) so a CollectionView fling cannot open unbounded HTTP work
- `IImageCache.TrimMemory` to compact the in-memory tier; energy saver turns this on automatically
- Display-time downsampling so CollectionView cells decode toward view/screen size instead of the full photo
- Cancellation of unused in-flight downloads when the last waiter (a recycled cell) lets go

### Fixed

- Recycled cells no longer keep a fade at opacity 0 or a decoded bitmap after handler disconnect
- Disk cache eviction uses a running size index instead of listing the cache directory after every write
- Power-saving mode skips automatic retries, fade-in, and shimmer so the radio and UI thread can idle
- `IImageLoader` is now a singleton so CollectionView cells share deduplication and the download cap
- Singleton loader creates an `HttpClient` per download via `IHttpClientFactory` so factory handlers can rotate
- Display downsample runs off the UI thread and retries when a CollectionView cell gets a real size
- `IImageCache.TrimMemory` has a default interface implementation so external implementers are not broken
- A cancelled waiter still receives an already-completed download; the in-flight join loop is bounded

## [1.0.5] - 2026-09-14

### Added

- Publish releases to GitHub Packages in addition to nuget.org (repo Packages tab)

## [1.0.4] - 2026-09-14

### Fixed

- Private memory cache so `ClearAsync` cannot wipe the host app's shared `IMemoryCache`
- Stale fade/load races no longer leave incorrect `State` or image source
- Concurrent disk writes use unique temp file names

### Changed

- Sample app branding aligned with package ID `SmartImage.Maui`

## [1.0.3] - 2026-09-14

### Changed

- NuGet package README: absolute image URLs, consumer-focused docs for nuget.org

## [1.0.2] - 2026-09-14

### Changed

- NuGet package ID is now `SmartImage.Maui` (`Maui.SmartImage` is reserved on nuget.org)

## [1.0.1] - 2026-09-14

### Removed

- Unpublished attempt under reserved package ID `Maui.SmartImage` (nuget.org 409); superseded by `SmartImage.Maui` 1.0.2+

## [1.0.0] - 2026-09-14

### Added

- `SmartImage` MAUI control with local/remote source detection, skeleton shimmer, fade-in, and retry overlay
- Two-tier `IImageCache` (memory + disk) with size limits, default TTL, atomic disk writes, and `ClearAsync` / `RemoveAsync`
- `IImageLoader` with in-flight deduplication, exponential backoff + jitter, and overall download budget
- `UseSmartImage()` DI registration with `HttpClient` factory and `SmartImageOptions`
- XML documentation, SourceLink, symbol package (`snupkg`), package icon, and MIT license expression
- Unit tests for cache, loader, classifier, and generation guard
- Sample app under `samples/`

### Changed

- Remote loads fail visibly when `UseSmartImage()` was not called
- HTTP retries limited to transient statuses (408, 429, 5xx); SSL errors are not retried
- Default max download size is 15 MB; default cache duration is 7 days
