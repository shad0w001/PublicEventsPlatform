using Domain.Events.Events;

namespace Application.Abstractions.Search;

public interface IEventPublishedEmbeddingHandler
{
    Task HandleAsync(EventPublished domainEvent, CancellationToken cancellationToken = default);
}
