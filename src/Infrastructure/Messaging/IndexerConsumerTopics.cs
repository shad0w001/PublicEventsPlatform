namespace Infrastructure.Messaging;

internal static class IndexerConsumerTopics
{
    public const string EventPublished = "domain.event-published";
    public const string EventUpdated = "domain.event-updated";
    public const string EventCancelled = "domain.event-cancelled";

    public static readonly string[] IndexerTopics =
    [
        EventPublished,
        EventUpdated,
        EventCancelled
    ];
}
