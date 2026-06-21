namespace Application.Events;

public sealed class EventOptions
{
    public const string SectionName = "Events";

    public int MaxPublishesPerHostPerWeek { get; init; } = 6;

    public string DefaultBannerUrl { get; init; } = "/images/default-event-banner.png";
}
