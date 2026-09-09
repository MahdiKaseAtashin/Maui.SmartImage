namespace Maui.SmartImage.Services;

public enum ImageLoadErrorKind
{
    InvalidUrl,
    Timeout,
    NetworkError,
    SslError,
    HttpError,
    TooLarge,
    CorruptedData,
    Unknown
}
