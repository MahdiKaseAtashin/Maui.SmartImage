using FluentAssertions;
using Maui.SmartImage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using Xunit;

namespace Maui.SmartImage.Tests;

public class MauiAppBuilderExtensionsTests : IDisposable
{
    private readonly string _diskCacheDirectory =
        Path.Combine(Path.GetTempPath(), "smart-image-di-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_diskCacheDirectory))
        {
            Directory.Delete(_diskCacheDirectory, recursive: true);
        }
    }

    [Fact]
    public void UseSmartImage_RegistersCacheAndLoader()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseSmartImage(options =>
        {
            options.DiskCacheDirectory = _diskCacheDirectory;
            options.DefaultTimeout = TimeSpan.FromSeconds(5);
            options.DefaultCacheDuration = TimeSpan.FromHours(1);
        });

        using MauiApp app = builder.Build();

        IImageCache cache = app.Services.GetRequiredService<IImageCache>();
        IImageLoader loader = app.Services.GetRequiredService<IImageLoader>();
        SmartImageOptions options = app.Services.GetRequiredService<SmartImageOptions>();

        cache.Should().NotBeNull().And.BeOfType<ImageCache>();
        loader.Should().NotBeNull().And.BeOfType<ImageLoader>();
        options.DiskCacheDirectory.Should().Be(_diskCacheDirectory);
        options.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(5));
        options.DefaultCacheDuration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void UseSmartImage_RegistersSingletonCacheInstance()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseSmartImage(options => options.DiskCacheDirectory = _diskCacheDirectory);

        using MauiApp app = builder.Build();

        IImageCache first = app.Services.GetRequiredService<IImageCache>();
        IImageCache second = app.Services.GetRequiredService<IImageCache>();

        first.Should().BeSameAs(second);
    }
}
