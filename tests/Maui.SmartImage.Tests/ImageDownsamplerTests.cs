using FluentAssertions;
using Maui.SmartImage.Services;
using Xunit;

namespace Maui.SmartImage.Tests;

public class ImageDownsamplerTests
{
    [Fact]
    public void UsesViewSize_WhenBothDimensionsPositive_IsTrue()
    {
        ImageDownsampler.UsesViewSize(120, 80).Should().BeTrue();
    }

    [Fact]
    public void UsesViewSize_WhenWidthOrHeightMissing_IsFalse()
    {
        ImageDownsampler.UsesViewSize(-1, 80).Should().BeFalse();
        ImageDownsampler.UsesViewSize(120, 0).Should().BeFalse();
        ImageDownsampler.UsesViewSize(0, 0).Should().BeFalse();
    }

    [Fact]
    public void ResolveMaxPixels_WhenViewSizeKnown_MarksUsedViewSize()
    {
        (_, _, bool usedViewSize) = ImageDownsampler.ResolveMaxPixels(100, 50);
        usedViewSize.Should().BeTrue();
    }

    [Fact]
    public void ResolveMaxPixels_WhenViewSizeUnknown_DoesNotMarkUsedViewSize()
    {
        (_, _, bool usedViewSize) = ImageDownsampler.ResolveMaxPixels(-1, -1);
        usedViewSize.Should().BeFalse();
    }
}
