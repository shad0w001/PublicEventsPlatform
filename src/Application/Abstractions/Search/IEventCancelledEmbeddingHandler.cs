using Domain.Events.Events;

namespace Application.Abstractions.Search;

public interface IEventCancelledEmbeddingHandler
{
    Task HandleAsync(EventCancelled domainEvent, CancellationToken cancellationToken = default);
}
