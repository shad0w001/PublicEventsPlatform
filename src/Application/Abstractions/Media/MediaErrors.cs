using SharedKernel;

namespace Application.Abstractions.Media;

public static class MediaErrors
{
    public static readonly Error EmptyFile = Error.Validation(
        "Media.EmptyFile",
        "The uploaded file is empty");

    public static Error FileTooLarge(long maxBytes) => Error.Validation(
        "Media.FileTooLarge",
        $"The uploaded file exceeds the maximum size of {maxBytes} bytes");

    public static readonly Error InvalidContentType = Error.Validation(
        "Media.InvalidContentType",
        "The uploaded file type is not allowed");

    public static readonly Error InvalidImage = Error.Validation(
        "Media.InvalidImage",
        "The uploaded file is not a valid image");

    public static readonly Error UnsupportedPurpose = Error.Validation(
        "Media.UnsupportedPurpose",
        "The requested media purpose is not supported");
}
