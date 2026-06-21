namespace Infrastructure.Messaging;

internal static class IndexerConsumerNames
{
    public const string EventPublished = "indexer.event-published";
    public const string EventUpdated = "indexer.event-updated";
    public const string EventCancelled = "indexer.event-cancelled";
}
