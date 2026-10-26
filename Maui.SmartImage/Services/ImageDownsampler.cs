using Microsoft.Maui.Devices;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using GraphicsImage = Microsoft.Maui.Graphics.IImage;

namespace Maui.SmartImage.Services;

/// <summary>
/// Shrinks decoded display payloads so a CollectionView cell does not keep a full-resolution bitmap.
/// Cache tiers still store the original bytes.
/// </summary>
internal static class ImageDownsampler
{
    public static byte[] MaybeDownsample(byte[] imageData, double viewWidth, double viewHeight)
    {
        if (imageData.Length == 0 || ImageSignature.IsGif(imageData))
        {
            return imageData;
        }

        try
        {
            (double maxWidth, double maxHeight, _) = ResolveMaxPixels(viewWidth, viewHeight);
            if (maxWidth <= 0 || maxHeight <= 0)
            {
                return imageData;
            }

            using MemoryStream input = new(imageData, writable: false);
            GraphicsImage? image = PlatformImage.FromStream(input);
            if (image is null)
            {
                return imageData;
            }

            try
            {
                if (image.Width <= maxWidth && image.Height <= maxHeight)
                {
                    return imageData;
                }

                GraphicsImage resized = image.Downsize((float)maxWidth, (float)maxHeight, disposeOriginal: false);
                try
                {
                    using MemoryStream output = new();
                    ImageFormat format = ImageSignature.PrefersPng(imageData)
                        ? ImageFormat.Png
                        : ImageFormat.Jpeg;
                    resized.Save(output, format, quality: 0.85f);
                    byte[] result = output.ToArray();
                    return result.Length > 0 && result.Length < imageData.Length ? result : imageData;
                }
                finally
                {
                    if (!ReferenceEquals(resized, image))
                    {
                        resized.Dispose();
                    }
                }
            }
            finally
            {
                image.Dispose();
            }
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception)
        {
            return imageData;
        }
    }

    internal static bool UsesViewSize(double viewWidth, double viewHeight)
    {
        return viewWidth > 0 && viewHeight > 0;
    }

    internal static (double Width, double Height, bool UsedViewSize) ResolveMaxPixels(double viewWidth, double viewHeight)
    {
        double density = 1;
        double screenWidth = 0;
        double screenHeight = 0;

        try
        {
            DisplayInfo info = DeviceDisplay.MainDisplayInfo;
            density = info.Density > 0 ? info.Density : 1;
            screenWidth = info.Width;
            screenHeight = info.Height;
        }
        catch (Exception)
        {
            // Headless tests and platforms without display APIs.
        }

        bool usedViewSize = UsesViewSize(viewWidth, viewHeight);
        double maxWidth = usedViewSize ? viewWidth * density : screenWidth;
        double maxHeight = usedViewSize ? viewHeight * density : screenHeight;
        return (maxWidth, maxHeight, usedViewSize);
    }
}
