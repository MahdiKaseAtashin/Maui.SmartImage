# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
