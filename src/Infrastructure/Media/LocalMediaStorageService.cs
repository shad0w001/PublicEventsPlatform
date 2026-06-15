using Application.Abstractions.Media;
using Application.Media;
using Microsoft.Extensions.Options;
using SharedKernel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;

namespace Infrastructure.Media;

internal sealed class LocalMediaStorageService(IOptions<MediaOptions> options) : IMediaStorageService
{
    private static readonly WebpEncoder WebpEncoder = new() { Quality = 85 };

    public async Task<Result<string>> SaveAsync(
        MediaPurpose purpose,
        Guid entityId,
        MediaUploadRequest upload,
        CancellationToken cancellationToken)
    {
        if (upload.ContentLength <= 0)
        {
            return Result.Failure<string>(MediaErrors.EmptyFile);
        }

        if (!TryGetProfile(purpose, out var profile))
        {
            return Result.Failure<string>(MediaErrors.UnsupportedPurpose);
        }

        if (upload.ContentLength > profile.MaxBytes)
        {
            return Result.Failure<string>(MediaErrors.FileTooLarge(profile.MaxMegabytes));
        }

        if (!IsAllowedContentType(upload.ContentType, profile.AllowedContentTypes))
        {
            return Result.Failure<string>(MediaErrors.InvalidContentType);
        }

        var webRoot = options.Value.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            throw new InvalidOperationException("MediaOptions.WebRootPath must be set at startup.");
        }

        Image image;
        try
        {
            image = await Image.LoadAsync(upload.Content, cancellationToken);
        }
        catch
        {
            return Result.Failure<string>(MediaErrors.InvalidImage);
        }

        using (image)
        {
            if (image.Width > profile.MaxWidth || image.Height > profile.MaxHeight)
            {
                return Result.Failure<string>(MediaErrors.DimensionsTooLarge(profile.MaxWidth, profile.MaxHeight));
            }

            var relativeUrl = BuildRelativeUrl(purpose, entityId);
            var physicalPath = MapRelativeUrlToPhysicalPath(webRoot, relativeUrl);

            var directory = Path.GetDirectoryName(physicalPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await using var output = File.Create(physicalPath);
            await image.SaveAsync(output, WebpEncoder, cancellationToken);

            return relativeUrl;
        }
    }

    public Task TryDeleteLocalFileAsync(string? url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.CompletedTask;
        }

        var webRoot = options.Value.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            return Task.CompletedTask;
        }

        var rootPath = options.Value.RootPath.Trim('/').Trim('\\');
        var uploadsPrefix = $"/{rootPath}/";
        if (!url.StartsWith(uploadsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        var physicalPath = MapRelativeUrlToPhysicalPath(webRoot, url);
        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }

    private bool TryGetProfile(MediaPurpose purpose, out MediaProfileOptions profile)
    {
        return options.Value.Profiles.TryGetValue(purpose.ToString(), out profile!);
    }

    private string BuildRelativeUrl(MediaPurpose purpose, Guid entityId)
    {
        var rootPath = options.Value.RootPath.Trim('/').Trim('\\');
        var folder = purpose switch
        {
            MediaPurpose.EventBanner => "events",
            MediaPurpose.GroupProfile => "groups",
            MediaPurpose.UserAvatar => "users",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };

        return $"/{rootPath}/{folder}/{entityId}/{Guid.NewGuid():N}.webp";
    }

    private static string MapRelativeUrlToPhysicalPath(string webRoot, string relativeUrl)
    {
        var relativePath = relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var combined = Path.GetFullPath(Path.Combine(webRoot, relativePath));
        var webRootFull = Path.GetFullPath(webRoot);

        if (!combined.StartsWith(webRootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid media path.");
        }

        return combined;
    }

    private static bool IsAllowedContentType(string contentType, string[] allowed)
    {
        if (allowed.Length == 0)
        {
            return false;
        }

        var normalized = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return allowed.Any(t => string.Equals(t.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }
}
