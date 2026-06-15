using SharedKernel;

namespace Application.Abstractions.Media;

public interface IMediaStorageService
{
    Task<Result<string>> SaveAsync(
        MediaPurpose purpose,
        Guid entityId,
        MediaUploadRequest upload,
        CancellationToken cancellationToken);

    Task TryDeleteLocalFileAsync(string? url, CancellationToken cancellationToken);
}
