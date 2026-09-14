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

Prefer unit tests for cache/loader/control behavior. Do not run `dotnet test` on the full solution unless Appium UI smoke is set up — the `tests/UITests/*` projects require a running Appium server and platform app paths.

## Pack

```sh
dotnet pack Maui.SmartImage/Maui.SmartImage.csproj -c Release -o artifacts
```

## UI smoke (Appium)

CI runs UI-state smoke on every PR via [`.github/workflows/smoke-ui.yml`](.github/workflows/smoke-ui.yml) for **Windows**, **Android**, **iOS**, and **MacCatalyst**. Tests assert `Loaded` / `Failed` / retry on `SmokePage` only (not shimmer/fade).

### Prerequisites

- Node.js 20+
- Appium 2: `npm install -g appium`
- Platform driver, for example:
  - Windows: `appium driver install windows` (Developer Mode / WinAppDriver as required by the driver)
  - Android: `appium driver install uiautomator2` + emulator/device
  - iOS: `appium driver install xcuitest` + simulator
  - MacCatalyst: `appium driver install mac2`

### Publish the sample in smoke mode

```sh
# Windows (example)
dotnet publish samples/Maui.SmartImage.Sample/Maui.SmartImage.Sample.csproj -c Release -f net10.0-windows10.0.19041.0 -p:SMARTIMAGE_SMOKE=true -p:WindowsPackageType=None -p:RuntimeIdentifierOverride=win-x64 -p:UseMonoRuntime=false -o artifacts/windows-app

# Android (example)
dotnet publish samples/Maui.SmartImage.Sample/Maui.SmartImage.Sample.csproj -c Release -f net10.0-android -p:SMARTIMAGE_SMOKE=true -p:TargetFrameworks=net10.0-android
```

`SMARTIMAGE_SMOKE=true` compiles the sample to open `SmokePage` (local + intentionally failed remote only). Locally you can also launch a normal build with `--smoke` or `SMARTIMAGE_SMOKE=1`.

### Run tests

Start Appium (`appium --port 4723`), then:

```sh
# Windows
set WINDOWS_APP_PATH=artifacts\windows-app\Maui.SmartImage.Sample.exe
dotnet test tests/UITests/UITests.Windows/UITests.Windows.csproj -c Release

# Android
export ANDROID_APP_PATH=/path/to/*-Signed.apk
dotnet test tests/UITests/UITests.Android/UITests.Android.csproj -c Release

# iOS
export IOS_APP_PATH=/path/to/Maui.SmartImage.Sample.app
dotnet test tests/UITests/UITests.iOS/UITests.iOS.csproj -c Release

# MacCatalyst
export MACCATALYST_APP_PATH=/path/to/Maui.SmartImage.Sample.app
dotnet test tests/UITests/UITests.MacCatalyst/UITests.MacCatalyst.csproj -c Release
```

## Guidelines

- Keep the public API intentional and documented (XML docs on public members).
- Prefer unit tests for cache/loader/control behavior over UI tests.
- Do not commit `bin/`, `obj/`, or local `artifacts/` packages.
- Open a PR against `main` (or the active development branch); CI and UI smoke must pass.

## Sample app

The sample under `samples/Maui.SmartImage.Sample` is the manual path for visual behavior (shimmer, fade, retry on `MainPage`). CI also compiles the sample for Windows and runs Appium UI-state smoke on all four platforms.
