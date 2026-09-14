# Contributing

Thanks for helping improve SmartImage.Maui.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MAUI workload: `dotnet workload install maui`

## Build and test

```sh
dotnet restore Maui.SmartImage.sln
dotnet build Maui.SmartImage.sln -c Release
dotnet test Maui.SmartImage.sln -c Release --no-build
```

## Pack

```sh
dotnet pack Maui.SmartImage/Maui.SmartImage.csproj -c Release -o artifacts
```

## Guidelines

- Keep the public API intentional and documented (XML docs on public members).
- Prefer unit tests for cache/loader behavior over UI tests.
- Do not commit `bin/`, `obj/`, or local `artifacts/` packages.
- Open a PR against `main` (or the active development branch); CI must pass.

## Sample app

The sample under `samples/Maui.SmartImage.Sample` is the manual smoke path for visual behavior (shimmer, fade, retry).
