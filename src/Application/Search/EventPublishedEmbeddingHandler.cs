using Application.Abstractions.Search;
using Application.Search.Services;
using Domain.Events.Events;

namespace Application.Search;

internal sealed class EventPublishedEmbeddingHandler(
    IEventEmbeddingIndexService indexService) : IEventPublishedEmbeddingHandler
{
    public Task HandleAsync(
        EventPublished domainEvent,
        CancellationToken cancellationToken = default) =>
        indexService.UpsertAsync(domainEvent.EventId, cancellationToken);
}
