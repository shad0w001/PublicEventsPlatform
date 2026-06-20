namespace Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; init; } = "localhost:9092";
    public string ClientId { get; init; } = "public-events-platform";
    public string ConsumerGroupId { get; init; } = "public-events-platform-notifications";
    public string CascadeConsumerGroupId { get; init; } = "public-events-platform-cascade";
    public string IndexerConsumerGroupId { get; init; } = "public-events-platform-indexer";
    public int OutboxPollIntervalSeconds { get; init; } = 5;
    public int OutboxBatchSize { get; init; } = 20;
}
