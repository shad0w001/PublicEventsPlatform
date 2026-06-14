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
    public long MaxBytes { get; init; }

    public int MaxWidth { get; init; }

    public int MaxHeight { get; init; }

    public string[] AllowedContentTypes { get; init; } = [];
}
