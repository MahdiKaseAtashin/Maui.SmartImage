# Contributing

Thanks for helping improve SmartImage.Maui.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MAUI workload: `dotnet workload install maui`

## Build and unit test

```sh
dotnet restore Maui.SmartImage/Maui.SmartImage.csproj
dotnet restore tests/Maui.SmartImage.Tests/Maui.SmartImage.Tests.csproj
dotnet build Maui.SmartImage/Maui.SmartImage.csproj -c Release
dotnet build tests/Maui.SmartImage.Tests/Maui.SmartImage.Tests.csproj -c Release
dotnet test tests/Maui.SmartImage.Tests/Maui.SmartImage.Tests.csproj -c Release --no-build
```

Prefer unit tests for cache/loader/control behavior. CI builds the library and unit tests, and compiles the sample for Windows.

## Pack

```sh
dotnet pack Maui.SmartImage/Maui.SmartImage.csproj -c Release -o artifacts
```

## Guidelines

- Keep the public API intentional and documented (XML docs on public members).
- Prefer unit tests for cache/loader/control behavior.
- Do not commit `bin/`, `obj/`, or local `artifacts/` packages.
- Open a PR against `main` (or the active development branch); CI must pass.

## Sample app

The sample under `samples/Maui.SmartImage.Sample` is the manual path for visual behavior (shimmer, fade, retry on `MainPage`). CI compiles the sample for Windows to catch XAML/API breakages.
