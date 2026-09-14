namespace Maui.SmartImage.Services;

/// <summary>
/// Result of a remote image load attempt.
/// </summary>
public sealed record ImageLoadResult
{
    /// <summary>
    /// Whether the load produced image bytes.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Image payload when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public byte[]? ImageData { get; init; }

    /// <summary>
    /// Failure classification when <see cref="IsSuccess"/> is <see langword="false"/>.
    /// </summary>
    public ImageLoadErrorKind? ErrorKind { get; init; }

    /// <summary>
    /// Optional human-readable failure detail.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// HTTP status code when <see cref="ErrorKind"/> is <see cref="ImageLoadErrorKind.HttpError"/>.
    /// </summary>
    public int? HttpStatusCode { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ImageLoadResult Success(byte[] imageData)
    {
        return new ImageLoadResult
        {
            IsSuccess = true,
            ImageData = imageData
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ImageLoadResult Failure(
        ImageLoadErrorKind errorKind,
        string? errorMessage = null,
        int? httpStatusCode = null)
    {
        return new ImageLoadResult
        {
            IsSuccess = false,
            ErrorKind = errorKind,
            ErrorMessage = errorMessage,
            HttpStatusCode = httpStatusCode
        };
    }
}
