namespace SharedKernel;

public interface IDomainEvent
{
    DateTime OccurredOnUtc { get; }
}
