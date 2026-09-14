namespace Maui.SmartImage.Services;

/// <summary>
/// Classifies a <see cref="Controls.SmartImage.Source"/> string as empty, local, or remote.
/// </summary>
internal static class ImageSourceClassifier
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
