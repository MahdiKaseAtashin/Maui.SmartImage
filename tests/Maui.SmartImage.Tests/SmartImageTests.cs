using FluentAssertions;
using Maui.SmartImage.Controls;
using Maui.SmartImage.Services;
using NSubstitute;
using Xunit;
using SmartImageControl = Maui.SmartImage.Controls.SmartImage;

namespace Maui.SmartImage.Tests;

public class SmartImageTests
{
    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00
    ];

    private const string RemoteUrl = "https://example.com/cover.png";

    public SmartImageTests()
    {
        MauiTestBootstrap.EnsureInitialized();
    }

    [Fact]
    public async Task EmptySource_SetsIdleWithoutCallingLoader()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        SmartImageControl image = CreateControl(loader);

        image.Source = null;
        await WaitForStateAsync(image, SmartImageState.Idle);

        image.State.Should().Be(SmartImageState.Idle);
        image.IsLoading.Should().BeFalse();
        image.IsFailed.Should().BeFalse();
        await loader.DidNotReceiveWithAnyArgs().LoadAsync(default!, default);
    }

    [Fact]
    public async Task LocalSource_SetsLoadedWithoutCallingLoader()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        SmartImageControl image = CreateControl(loader);

        image.Source = "dotnet_bot.png";
        await WaitForStateAsync(image, SmartImageState.Loaded);

        image.State.Should().Be(SmartImageState.Loaded);
        AssertFileSource(image.GetDisplayedSourceForTests(), "dotnet_bot.png");
        await loader.DidNotReceiveWithAnyArgs().LoadAsync(default!, default);
    }

    [Fact]
    public async Task RemoteSource_OnSuccess_SetsLoaded()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadAsync(Arg.Any<ImageLoadRequest>(), Arg.Any<CancellationToken>())
            .Returns(ImageLoadResult.Success(ValidPngBytes));

        SmartImageControl image = CreateControl(loader);
        image.Source = RemoteUrl;

        await WaitForStateAsync(image, SmartImageState.Loaded);

        image.State.Should().Be(SmartImageState.Loaded);
        image.Error.Should().BeNull();
        image.GetDisplayedSourceForTests().Should().NotBeNull();
        await loader.Received(1).LoadAsync(
            Arg.Is<ImageLoadRequest>(r => r.Url == RemoteUrl),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoteSource_OnFailure_SetsFailedAndAllowsRetry()
    {
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadAsync(Arg.Any<ImageLoadRequest>(), Arg.Any<CancellationToken>())
            .Returns(
                ImageLoadResult.Failure(ImageLoadErrorKind.HttpError, "not found", 404),
                ImageLoadResult.Success(ValidPngBytes));

        SmartImageControl image = CreateControl(loader);
        image.Source = RemoteUrl;

        await WaitForStateAsync(image, SmartImageState.Failed);

        image.State.Should().Be(SmartImageState.Failed);
        image.IsFailed.Should().BeTrue();
        image.Error.Should().Be("not found");
        image.RetryCommand.CanExecute(null).Should().BeTrue();

        image.RetryCommand.Execute(null);
        await WaitForStateAsync(image, SmartImageState.Loaded);

        image.State.Should().Be(SmartImageState.Loaded);
        await loader.Received(2).LoadAsync(Arg.Any<ImageLoadRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MissingUseSmartImage_WithRemoteSource_SetsFailedWithRegistrationError()
    {
        SmartImageControl image = new()
        {
            EnableFadeAnimation = false,
            Source = RemoteUrl
        };

        image.AttachForTests(imageLoader: null);

        await WaitForStateAsync(image, SmartImageState.Failed);

        image.State.Should().Be(SmartImageState.Failed);
        image.Error.Should().Be(SmartImageControl.MissingRegistrationError);
    }

    [Fact]
    public async Task KeepPreviousImageWhileLoading_DoesNotClearDisplayedSource()
    {
        TaskCompletionSource<ImageLoadResult> remoteLoad = new();
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadAsync(Arg.Any<ImageLoadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => remoteLoad.Task);

        SmartImageControl image = CreateControl(loader);
        image.Source = "dotnet_bot.png";
        await WaitForStateAsync(image, SmartImageState.Loaded);

        ImageSource? previousSource = image.GetDisplayedSourceForTests();
        AssertFileSource(previousSource, "dotnet_bot.png");

        image.KeepPreviousImageWhileLoading = true;
        image.Source = RemoteUrl;

        await WaitForStateAsync(image, SmartImageState.Loading);
        image.GetDisplayedSourceForTests().Should().BeSameAs(previousSource);

        remoteLoad.SetResult(ImageLoadResult.Success(ValidPngBytes));
        await WaitForStateAsync(image, SmartImageState.Loaded);
    }

    [Fact]
    public async Task WithoutKeepPrevious_ClearsToPlaceholderWhileLoading()
    {
        TaskCompletionSource<ImageLoadResult> remoteLoad = new();
        IImageLoader loader = Substitute.For<IImageLoader>();
        loader.LoadAsync(Arg.Any<ImageLoadRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => remoteLoad.Task);

        FileImageSource placeholder = new() { File = "placeholder.png" };
        SmartImageControl image = CreateControl(loader);
        image.Placeholder = placeholder;
        image.Source = "dotnet_bot.png";
        await WaitForStateAsync(image, SmartImageState.Loaded);

        image.KeepPreviousImageWhileLoading = false;
        image.Source = RemoteUrl;

        await WaitForStateAsync(image, SmartImageState.Loading);
        image.GetDisplayedSourceForTests().Should().BeSameAs(placeholder);

        remoteLoad.SetResult(ImageLoadResult.Success(ValidPngBytes));
        await WaitForStateAsync(image, SmartImageState.Loaded);
    }

    private static SmartImageControl CreateControl(IImageLoader loader)
    {
        SmartImageControl image = new()
        {
            EnableFadeAnimation = false
        };
        image.AttachForTests(loader);
        return image;
    }

    private static void AssertFileSource(ImageSource? source, string expectedFile)
    {
        source.Should().BeOfType<FileImageSource>()
            .Which.File.Should().Be(expectedFile);
    }

    private static async Task WaitForStateAsync(
        SmartImageControl image,
        SmartImageState expected,
        TimeSpan? timeout = null)
    {
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(3);
        DateTimeOffset deadline = DateTimeOffset.UtcNow + limit;

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (image.State == expected)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"Timed out waiting for SmartImage.State == {expected}. Actual state: {image.State}, Error: {image.Error}");
    }
}
