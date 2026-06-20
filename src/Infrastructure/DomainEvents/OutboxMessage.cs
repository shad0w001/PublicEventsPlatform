namespace Infrastructure.DomainEvents;

public sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public string EventType { get; init; } = null!;
    public string Topic { get; init; } = null!;
    public string Payload { get; init; } = null!;
    public DateTime OccurredOnUtc { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? PublishedAt { get; set; }
    public string? Error { get; set; }
}
