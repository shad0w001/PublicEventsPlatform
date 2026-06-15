namespace Application.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    public string RootPath { get; init; } = "uploads";

    /// <summary>Set at startup from the web host (not from appsettings).</summary>
    public string WebRootPath { get; set; } = string.Empty;

    public Dictionary<string, MediaProfileOptions> Profiles { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class MediaProfileOptions
{
    public int MaxMegabytes { get; init; }

    public long MaxBytes => MaxMegabytes * 1024L * 1024L;

    public int MaxWidth { get; init; }

    public int MaxHeight { get; init; }

    public string[] AllowedContentTypes { get; init; } = [];
}
