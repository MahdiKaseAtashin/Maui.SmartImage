namespace Maui.SmartImage.Services;

public static class ImageSourceClassifier
{
    public static ImageSourceKind Classify(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return ImageSourceKind.Empty;
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out Uri? uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ImageSourceKind.Remote;
        }

        return ImageSourceKind.Local;
    }
}
