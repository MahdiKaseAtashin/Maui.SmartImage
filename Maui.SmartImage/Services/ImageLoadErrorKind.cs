namespace Maui.SmartImage.Services;

/// <summary>
/// Classifies why a remote image load failed.
/// </summary>
public enum ImageLoadErrorKind
{
    /// <summary>The URL was missing or not http(s).</summary>
    InvalidUrl,

    /// <summary>The per-attempt or overall timeout elapsed.</summary>
    Timeout,

    /// <summary>A transport-level network failure occurred.</summary>
    NetworkError,

    /// <summary>TLS/SSL negotiation failed.</summary>
    SslError,

    /// <summary>The server returned a non-success HTTP status.</summary>
    HttpError,

    /// <summary>The response exceeded the configured size limit.</summary>
    TooLarge,

    /// <summary>The payload was not a recognized image format.</summary>
    CorruptedData,

    /// <summary>An unclassified failure occurred.</summary>
    Unknown
}
