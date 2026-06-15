namespace Application.Abstractions.Media;

public sealed record MediaUploadRequest(
    Stream Content,
    string ContentType,
    long ContentLength,
    string? FileName);
