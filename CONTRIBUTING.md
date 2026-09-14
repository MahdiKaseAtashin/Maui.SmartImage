# Contributing

Thanks for helping improve SmartImage.Maui.

## Issues and pull requests

- Bugs, features, and docs: use the [issue forms](https://github.com/MahdiKaseAtashin/Maui.SmartImage/issues/new/choose).
- Security: see [SECURITY.md](.github/SECURITY.md) — do not file a public issue.
- Open a PR against `main`. Use the pull request template. **CI (`build-test`) must stay green.**
- Labels are defined in [`.github/labels.yml`](.github/labels.yml). PRs are auto-labeled by path.

## Git attribution

Commits are authored by the human contributor only. Do not add `Co-authored-by: Cursor` or other AI/agent trailers.

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

Production packs run from git tags `v*` (see `.github/workflows/pack.yml`).

## Guidelines

- Keep the public API intentional and documented (XML docs on public members).
- Prefer unit tests for cache/loader/control behavior.
- Do not commit `bin/`, `obj/`, or local `artifacts/` packages.
- Breaking changes need the `breaking-change` label and a CHANGELOG entry.

## Sample app

The sample under `samples/Maui.SmartImage.Sample` is the manual path for visual behavior (shimmer, fade, retry on `MainPage`). CI compiles the sample for Windows to catch XAML/API breakages.

## Repository rulesets

Branch and tag protection JSON lives in [`.github/rulesets/`](.github/rulesets/README.md). Apply with `gh api` as a repo admin (GitHub does not read those files by itself).
