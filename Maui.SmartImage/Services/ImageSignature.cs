namespace Maui.SmartImage.Services;

/// <summary>
/// Shared magic-byte checks for common raster image formats.
/// </summary>
internal static class ImageSignature
{
    public static bool IsRecognizedImage(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4)
        {
            return false;
        }

        if (data.Length >= 8 &&
            data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
            data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A)
        {
            return true; // PNG
        }

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return true; // JPEG
        }

        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
        {
            return true; // GIF87a / GIF89a
        }

        if (data[0] == 0x42 && data[1] == 0x4D)
        {
            return true; // BMP
        }

        if (data.Length >= 12 &&
            data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
        {
            return true; // WebP (RIFF....WEBP)
        }

        return false;
    }
}
