namespace Application.Groups;

public sealed class GroupProfileOptions
{
    public const string SectionName = "GroupProfile";

    public string DefaultImageUrl { get; init; } = "/images/default-group.png";
}
