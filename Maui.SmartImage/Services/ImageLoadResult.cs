namespace Maui.SmartImage.Services;

public sealed record ImageLoadResult
{
    public required bool IsSuccess { get; init; }

    public byte[]? ImageData { get; init; }

    public ImageLoadErrorKind? ErrorKind { get; init; }

    public string? ErrorMessage { get; init; }

    public static ImageLoadResult Success(byte[] imageData)
    {
        return new ImageLoadResult
        {
            IsSuccess = true,
            ImageData = imageData
        };
    }

    public static ImageLoadResult Failure(ImageLoadErrorKind errorKind, string? errorMessage = null)
    {
        return new ImageLoadResult
        {
            IsSuccess = false,
            ErrorKind = errorKind,
            ErrorMessage = errorMessage
        };
    }
}
