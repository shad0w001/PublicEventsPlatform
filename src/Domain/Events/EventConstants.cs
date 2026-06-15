namespace Domain.Events;

public static class EventConstants
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 5000;
    public const int SmallTierMaxLocations = 2;

    /// <summary>Placeholder for draft events until real times are set via PATCH.</summary>
    public static readonly DateTime DraftEpochUtc = DateTime.UnixEpoch;
}
