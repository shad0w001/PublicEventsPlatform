namespace Infrastructure.DomainEvents;

public sealed class ProcessedMessage
{
    public string ConsumerName { get; init; } = null!;
    public Guid MessageId { get; init; }
    public DateTime ProcessedAt { get; init; }
}
