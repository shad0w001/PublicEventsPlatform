namespace Application.Users;

public sealed class UserProfileOptions
{
    public const string SectionName = "UserProfile";

    public string DefaultAvatarUrl { get; init; } = "/images/default-avatar.png";
}
