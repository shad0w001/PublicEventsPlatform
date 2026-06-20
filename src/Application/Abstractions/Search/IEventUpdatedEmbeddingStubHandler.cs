using Domain.Events.Events;

namespace Application.Abstractions.Search;

public interface IEventUpdatedEmbeddingStubHandler
{
    Task HandleAsync(EventUpdated domainEvent, CancellationToken cancellationToken = default);
}
