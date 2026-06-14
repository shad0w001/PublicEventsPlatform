using Application.Abstractions.Media;
using Application.Media;
using Infrastructure.Media;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace InfrastructureTests.Media;

public class LocalMediaStorageServiceTests : IDisposable
{
    private readonly string _webRoot;
    private readonly LocalMediaStorageService _service;

    public LocalMediaStorageServiceTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "pep-media-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_webRoot);

        var options = Options.Create(new MediaOptions
        {
            RootPath = "uploads",
            WebRootPath = _webRoot,
            Profiles = new Dictionary<string, MediaProfileOptions>
            {
                ["EventBanner"] = new()
                {
                    MaxMegabytes = 5,
                    MaxWidth = 1920,
                    MaxHeight = 1080,
                    AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"]
                },
                ["GroupProfile"] = new()
                {
                    MaxMegabytes = 2,
                    MaxWidth = 800,
                    MaxHeight = 800,
                    AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"]
                }
            }
        });

        _service = new LocalMediaStorageService(options);
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LocalMediaStorageService_Should_ReturnFileTooLarge_When_ContentExceedsProfileLimit()
    {
        // Arrange
        await using var content = await CreateJpegStreamAsync(10, 10);
        var upload = new MediaUploadRequest(content, "image/jpeg", 3_000_000, "large.jpg");
        var entityId = Guid.NewGuid();

        var options = Options.Create(new MediaOptions
        {
            RootPath = "uploads",
            WebRootPath = _webRoot,
            Profiles = new Dictionary<string, MediaProfileOptions>
            {
                ["GroupProfile"] = new()
                {
                    MaxMegabytes = 0,
                    MaxWidth = 800,
                    MaxHeight = 800,
                    AllowedContentTypes = ["image/jpeg"]
                }
            }
        });
        var service = new LocalMediaStorageService(options);

        // Act
        var result = await service.SaveAsync(
            MediaPurpose.GroupProfile,
            entityId,
            upload,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Media.FileTooLarge", result.Error.Code);
        Assert.Contains("0 MB", result.Error.Message);
    }

    [Fact]
    public async Task LocalMediaStorageService_Should_ReturnInvalidContentType_When_TypeNotAllowed()
    {
        // Arrange
        await using var content = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        var upload = new MediaUploadRequest(content, "application/pdf", 4, "doc.pdf");
        var entityId = Guid.NewGuid();

        // Act
        var result = await _service.SaveAsync(
            MediaPurpose.EventBanner,
            entityId,
            upload,
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Media.InvalidContentType", result.Error.Code);
    }

    [Fact]
    public async Task LocalMediaStorageService_Should_SaveWebpFile_When_JpegIsValid()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        await using var content = await CreateJpegStreamAsync(640, 360);

        // Act
        var result = await _service.SaveAsync(
            MediaPurpose.EventBanner,
            entityId,
            new MediaUploadRequest(content, "image/jpeg", content.Length, "banner.jpg"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.StartsWith($"/uploads/events/{entityId}/", result.Value);
        Assert.EndsWith(".webp", result.Value);

        var physicalPath = Path.Combine(_webRoot, result.Value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));
    }

    [Fact]
    public async Task LocalMediaStorageService_Should_ReturnDimensionsTooLarge_When_DimensionsExceedProfileMax()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        await using var content = await CreateJpegStreamAsync(3000, 2000);

        // Act
        var result = await _service.SaveAsync(
            MediaPurpose.EventBanner,
            entityId,
            new MediaUploadRequest(content, "image/jpeg", content.Length, "big.jpg"),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("Media.DimensionsTooLarge", result.Error.Code);
        Assert.Contains("1920×1080", result.Error.Message);
    }

    [Fact]
    public async Task LocalMediaStorageService_Should_DeleteLocalUpload_When_UrlIsUnderUploadsRoot()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        await using var content = await CreateJpegStreamAsync(50, 50);
        var saveResult = await _service.SaveAsync(
            MediaPurpose.GroupProfile,
            entityId,
            new MediaUploadRequest(content, "image/jpeg", content.Length, "profile.jpg"),
            CancellationToken.None);
        Assert.True(saveResult.IsSuccess);

        var physicalPath = Path.Combine(_webRoot, saveResult.Value.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));

        // Act
        await _service.TryDeleteLocalFileAsync(saveResult.Value, CancellationToken.None);

        // Assert
        Assert.False(File.Exists(physicalPath));
    }

    [Fact]
    public async Task LocalMediaStorageService_Should_NotDelete_When_UrlIsDefaultStaticAsset()
    {
        // Arrange
        var defaultPath = Path.Combine(_webRoot, "images", "default-group.png");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        await File.WriteAllTextAsync(defaultPath, "keep");

        // Act
        await _service.TryDeleteLocalFileAsync("/images/default-group.png", CancellationToken.None);

        // Assert
        Assert.True(File.Exists(defaultPath));
    }

    private static async Task<MemoryStream> CreateJpegStreamAsync(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream);
        stream.Position = 0;
        return stream;
    }
}
