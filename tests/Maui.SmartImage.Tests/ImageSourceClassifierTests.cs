using Xunit;
using Maui.SmartImage.Services;
using FluentAssertions;

namespace Maui.SmartImage.Tests;

public class ImageSourceClassifierTests
{
    [Fact]
    public void Classify_WithNull_ReturnsEmpty()
    {
        ImageSourceKind result = ImageSourceClassifier.Classify(null);

        result.Should().Be(ImageSourceKind.Empty);
    }

    [Fact]
    public void Classify_WithEmptyString_ReturnsEmpty()
    {
        ImageSourceKind result = ImageSourceClassifier.Classify(string.Empty);

        result.Should().Be(ImageSourceKind.Empty);
    }

    [Fact]
    public void Classify_WithWhitespace_ReturnsEmpty()
    {
        ImageSourceKind result = ImageSourceClassifier.Classify("   ");

        result.Should().Be(ImageSourceKind.Empty);
    }

    [Theory]
    [InlineData("http://example.com/cover.jpg")]
    [InlineData("https://example.com/cover.jpg")]
    [InlineData("https://example.com/cover.jpg?X-Amz-Expires=2700&X-Amz-Signature=abc")]
    public void Classify_WithHttpOrHttpsUrl_ReturnsRemote(string source)
    {
        ImageSourceKind result = ImageSourceClassifier.Classify(source);

        result.Should().Be(ImageSourceKind.Remote);
    }

    [Theory]
    [InlineData("logo.png")]
    [InlineData("ic_lock")]
    [InlineData("/data/user/0/dev.bobthephysio.mobile/files/image.jpg")]
    [InlineData("ftp://example.com/cover.jpg")]
    public void Classify_WithLocalResourceOrFileOrNonHttpScheme_ReturnsLocal(string source)
    {
        ImageSourceKind result = ImageSourceClassifier.Classify(source);

        result.Should().Be(ImageSourceKind.Local);
    }
}
