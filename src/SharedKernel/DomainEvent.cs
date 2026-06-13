namespace SharedKernel;

public abstract record DomainEvent : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
