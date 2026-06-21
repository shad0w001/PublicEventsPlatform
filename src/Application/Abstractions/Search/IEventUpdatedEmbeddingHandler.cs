using Domain.Events.Events;

namespace Application.Abstractions.Search;

public interface IEventUpdatedEmbeddingHandler
{
    Task HandleAsync(EventUpdated domainEvent, CancellationToken cancellationToken = default);
}
