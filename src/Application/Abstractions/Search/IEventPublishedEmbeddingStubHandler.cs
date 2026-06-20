using Domain.Events.Events;

namespace Application.Abstractions.Search;

public interface IEventPublishedEmbeddingStubHandler
{
    Task HandleAsync(EventPublished domainEvent, CancellationToken cancellationToken = default);
}
