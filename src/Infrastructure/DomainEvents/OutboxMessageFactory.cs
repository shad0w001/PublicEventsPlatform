using SharedKernel;

namespace Infrastructure.DomainEvents;

internal static class OutboxMessageFactory
{
    public static OutboxMessage? CreateFromDomainEvent(IDomainEvent domainEvent)
    {
        if (!DomainEventTopicMapper.TryGetTopic(domainEvent, out var topic))
        {
            return null;
        }

        var occurredOnUtc = domainEvent is DomainEvent tracked
            ? tracked.OccurredOnUtc
            : DateTime.UtcNow;

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            EventType = domainEvent.GetType().Name,
            Topic = topic,
            Payload = DomainEventOutboxSerializer.Serialize(domainEvent),
            OccurredOnUtc = occurredOnUtc,
            CreatedAt = DateTime.UtcNow
        };
    }
}
