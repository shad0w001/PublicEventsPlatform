namespace Application.Events;

public sealed class EventOptions
{
    public const string SectionName = "Events";

    public int MaxPublishesPerHostPerWeek { get; init; } = 6;
}
