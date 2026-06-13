namespace Domain.Groups;

public static class GroupConstants
{
    public const int NameMaxLength = 80;
    public const int DescriptionMaxLength = 1000;
    public static readonly TimeSpan ReapplyCooldown = TimeSpan.FromHours(1);
}
